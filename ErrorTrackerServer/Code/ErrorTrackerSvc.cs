using BPUtil;
using BPUtil.SimpleHttp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	public partial class ErrorTrackerSvc : ServiceBase
	{
		static WebServer srv;
		Thread thrMaintainProjects;
		public static string SvcName { get; private set; }
		public ErrorTrackerSvc()
		{
			Settings.data.Load();

			Logger.CatchAll((sender, e) =>
			{
				Emailer.SendError(null, sender, e);
			});

			Settings.data.SaveIfNoExist();

			if (Settings.data.CountUsers() == 0)
			{
				User defaultAdmin = new User("admin", "admin", null, true)
				{
					Permanent = true
				};
				Settings.data.TryAddUser(defaultAdmin);
				defaultAdmin.InitializeUserId();
				Settings.data.Save();
			}
			if (string.IsNullOrWhiteSpace(Settings.data.systemName))
			{
				Settings.data.systemName = "Error Tracker";
				Settings.data.Save();
			}
			if (string.IsNullOrWhiteSpace(Settings.data.privateSigningKey))
			{
				Settings.data.privateSigningKey = new SignatureFactory().ExportPrivateKey();
				Settings.data.Save();
			}
			if (string.IsNullOrWhiteSpace(Settings.data.vapidPrivateKey)
				|| string.IsNullOrWhiteSpace(Settings.data.vapidPublicKey))
			{
				WebPush.VapidDetails vapidKeys = WebPush.VapidHelper.GenerateVapidKeys();
				Settings.data.vapidPrivateKey = vapidKeys.PrivateKey;
				Settings.data.vapidPublicKey = vapidKeys.PublicKey;
				foreach (User u in Settings.data.GetAllUsers())
					u.ClearAllPushNotificationSubscriptions();
				Settings.data.Save();
			}
			if (string.IsNullOrWhiteSpace(Settings.data.postgresBinPath))
			{
				try
				{
					Process postgresProcess = Process.GetProcessesByName("postgres").FirstOrDefault();
					if (postgresProcess != null)
					{
						FileInfo postgresExe = new FileInfo(postgresProcess.MainModule.FileName);
						Settings.data.postgresBinPath = postgresExe.Directory.FullName;
					}
				}
				catch { } // Can throw Win32Exception if we don't have enough permission
				if (!string.IsNullOrWhiteSpace(Settings.data.postgresBinPath))
					Settings.data.Save();
			}
			BPUtil.PasswordReset.StatelessPasswordResetBase.Initialize(Settings.data.privateSigningKey);

			// Initialize User IDs.
			bool setAny = false;
			foreach (User user in Settings.data.GetAllUsers())
				if (user.InitializeUserId())
					setAny = true;
			if (setAny)
				Settings.data.Save();

			InitializeComponent();

			SvcName = this.ServiceName;

			ICertificateSelector certSelector = null;
			if (!string.IsNullOrWhiteSpace(Settings.data.certificatePath) && File.Exists(Settings.data.certificatePath))
			{
				X509Certificate2 cert;
				if (!string.IsNullOrWhiteSpace(Settings.data.certificatePassword))
					cert = new X509Certificate2(Settings.data.certificatePath, Settings.data.certificatePassword);
				else
					cert = new X509Certificate2(Settings.data.certificatePath);
				certSelector = SimpleCertificateSelector.FromCertificate(cert);
			}
			srv = new WebServer(certSelector);

			thrMaintainProjects = new Thread(maintainProjects);
			thrMaintainProjects.Name = "Maintain Projects";
			thrMaintainProjects.IsBackground = false;
		}
		internal static void ConfigureWebserverLogging()
		{
			srv?.EnableLogging(Settings.data.webServerVerboseLogging);
		}
		protected override void OnStart(string[] args)
		{
			Logger.Info(ServiceName + " " + Globals.AssemblyVersion + " is starting.");
			thrMaintainProjects.Start();
			srv.SetBindings(Settings.data.port_http, Settings.data.port_https);
		}

		protected override void OnStop()
		{
			Logger.Info(ServiceName + " " + Globals.AssemblyVersion + " is stopping.");
			srv.Stop();
			thrMaintainProjects.Abort();
		}
		/// <summary>
		/// Current status of DB migration, or null if migration is done.
		/// </summary>
		public static bool LoadingDatabases { get; private set; } = true;
		public static string CurrentMaintenanceMessage = "Service is loading…";
		private void maintainProjects()
		{
			try
			{
				while (true)
				{
					try
					{
						foreach (Project p in Settings.data.GetAllProjects())
						{
							int maxAgeDays = p.MaxEventAgeDays;
							if (maxAgeDays > 0)
							{
								long ageCutoff = TimeUtil.GetTimeInMsSinceEpoch(DateTime.UtcNow.AddDays(-1 * maxAgeDays));
								using (DB db = new DB(p.Name))
								{
									db.DeleteEventsOlderThan(ageCutoff);
								}
							}
						}
						LoadingDatabases = false;
						CurrentMaintenanceMessage = "Service is ready.";
					}
					catch (ThreadAbortException) { throw; }
					catch (Exception ex)
					{
						CurrentMaintenanceMessage = "DB error: " + ex.Message;
						Logger.Debug(ex);
						Emailer.SendError(null, "Error when maintaining projects", ex);
					}

					if (LoadingDatabases)
						Thread.Sleep(TimeSpan.FromMinutes(0.5));
					else
					{
						try
						{
							BackupManager.BackupNow();
						}
						catch (ThreadAbortException) { throw; }
						catch (Exception ex)
						{
							Logger.Debug(ex);
							Emailer.SendError(null, "Error in BackupManager.RunTasks", ex);
						}

						Thread.Sleep(TimeSpan.FromMinutes(30));
					}
				}
			}
			catch (ThreadAbortException) { }
			catch (Exception ex)
			{
				Logger.Debug(ex, "Maintain Projects thread is exiting prematurely.");
				Emailer.SendError(null, "Maintain Projects thread is exiting prematurely.", ex);
			}
		}
	}
}

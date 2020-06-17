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
		WebServer srv;
		Thread thrMaintainProjects;
		public static string SvcName { get; private set; }
		public ErrorTrackerSvc()
		{
			Settings.data.Load();
			Settings.data.SaveIfNoExist();
			if (Settings.data.CountUsers() == 0)
			{
				User defaultAdmin = new User("admin", "admin", null, true)
				{
					Permanent = true
				};
				Settings.data.TryAddUser(defaultAdmin);
				Settings.data.Save();
			}

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
			SimpleHttpLogger.RegisterLogger(Logger.httpLogger);
			srv = new WebServer(Settings.data.port_http, Settings.data.port_https, certSelector, IPAddress.Any);

			thrMaintainProjects = new Thread(maintainProjects);
			thrMaintainProjects.Name = "Maintain Projects";
			thrMaintainProjects.IsBackground = false;
		}

		protected override void OnStart(string[] args)
		{
			srv.Start();
			thrMaintainProjects.Start();
		}

		protected override void OnStop()
		{
			srv.Stop();
			thrMaintainProjects.Abort();
		}

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
					}
					catch (ThreadAbortException) { throw; }
					catch (Exception ex)
					{
						Logger.Debug(ex);
					}
					Thread.Sleep(TimeSpan.FromMinutes(30));
				}
			}
			catch (ThreadAbortException) { }
			catch (Exception ex)
			{
				Logger.Debug(ex, "Maintain Projects thread is exiting prematurely.");
			}
		}
	}
}

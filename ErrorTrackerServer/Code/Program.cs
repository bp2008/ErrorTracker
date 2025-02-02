﻿using BPUtil;
using BPUtil.Forms;
using ErrorTrackerServer.Code;
using ErrorTrackerServer.Database.Creation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ErrorTrackerServer
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main()
		{
			System.Reflection.Assembly assembly = System.Reflection.Assembly.GetEntryAssembly();
			Globals.Initialize(assembly.Location, "Data/");
			Directory.CreateDirectory(Globals.WritableDirectoryBase + "Projects/");

			WindowsServiceInitOptions options = new WindowsServiceInitOptions();
			options.ServiceManagerButtons = new ButtonDefinition[]
			{
				new ButtonDefinition("Configure PostgreSQL", configureDb),
				new ButtonDefinition("Backup Database", dbBackup),
				new ButtonDefinition("Restore Database", dbRestore),
				new ButtonDefinition("Migrate From SQLite", migrateSqlite)
			};
			AppInit.WindowsService<ErrorTrackerSvc>(options); // Most of the initialization work happens here, including loading of the Settings.data object. The method blocks, so further initialization should be done in the ErrorTrackerSvc constructor.
		}

		private static void configureDb(object sender, EventArgs e)
		{
			if (!string.IsNullOrEmpty(Settings.data.postgresPassword))
			{
				if (DialogResult.Yes == MessageBox.Show("Your current configuration shows that PostgreSQL was already configured.  If this is incorrect (such as if you are starting fresh with a new database) you can click Yes to delete the currently saved configuration and open the database setup form.", "Restart configuration?", MessageBoxButtons.YesNoCancel))
				{
					if (DialogResult.Yes == MessageBox.Show("Continuing to reset the database configuration may result in data loss.  Continue?", "DATA LOSS WARNING", MessageBoxButtons.YesNoCancel))
					{
					}
					else
					{
						MessageBox.Show("Database setup aborted.");
						return;
					}
				}
				else
				{
					MessageBox.Show("Database setup aborted.");
					return;
				}
			}

			try
			{
				DatabaseSetupForm setupForm = new DatabaseSetupForm();
				if (setupForm.ShowDialog() == DialogResult.OK)
				{
					Settings.data.postgresHost = setupForm.PostgresHost;
					Settings.data.postgresPort = setupForm.PostgresPort;
					DbCreation.CreateInitialErrorTrackerDb(setupForm.PostgresUser, setupForm.PostgresPass, setupForm.PostgresDB);
					Settings.data.postgresReady = true;
					Settings.data.Save();
					MessageBox.Show("Database setup succeeded.");
					if (Settings.data.CountProjects() > 0)
						MessageBox.Show("Please notice that you have " + Settings.data.CountProjects() + " projects already configured in your configuration file.");
				}
				else
					MessageBox.Show("Database setup aborted.");
			}
			catch (Exception ex)
			{
				MessageBox.Show("Database setup failed." + Environment.NewLine + Environment.NewLine + ex.FlattenMessages());
			}
		}
		private static void migrateSqlite(object sender, EventArgs e)
		{
			MessageBox.Show("SQLite migration was removed from ErrorTracker 2.8.  Please use ErrorTracker 2.7.1 temporarily if you need to migrate SQLite database files to PostgreSQL.");
		}
		private static void dbBackup(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(Settings.data.postgresPassword))
			{
				MessageBox.Show("Please use the \"Configure PostgreSQL\" button first.");
				return;
			}

			DatabaseBackupForm f = new DatabaseBackupForm();
			f.ShowDialog();
		}
		private static void dbRestore(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(Settings.data.postgresPassword))
			{
				MessageBox.Show("Please use the \"Configure PostgreSQL\" button first.");
				return;
			}

			DatabaseRestoreForm f = new DatabaseRestoreForm();
			f.ShowDialog();
		}
	}
}

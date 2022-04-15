using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using BPUtil;
using ErrorTrackerServer.Database.Creation;

namespace ErrorTrackerServer
{
	/// <summary>
	/// Manages backing up and restoring the PostgreSQL database.  This backup system uses "plain" format because 7zip can compress it substantially better than "custom" format with built-in compression.
	/// </summary>
	public static class BackupManager
	{
		/// <summary>
		/// Called periodically by external code to initiate any necessary backups.
		/// </summary>
		/// <param name="backupName">If null, a daily backup will be created.  If not null, the backup will be named [backupName + ".7z"].</param>
		public static void BackupNow(string backupName = null)
		{
			bool isManualBackup = backupName != null;
			try
			{
				if (isManualBackup)
				{
					if (Regex.IsMatch(backupName, @"^(\d\d\d\d)-(\d\d)-(\d\d)$"))
						throw new BackupException("Manual backup name cannot match the \"YYYY-MM-DD\" pattern as it would interfere with backup pruning.");
				}
				else
					backupName = GetFilenameForDate(DateTime.Now);

				if (!Settings.data.backup)
				{
					if (isManualBackup)
						throw new BackupException("Backups are not configured.");
					return;
				}
				if (string.IsNullOrEmpty(Settings.data.postgresPassword))
					throw new BackupException("Backups are enabled but the PostgreSQL database has not been initialized.");
				// Create backups
				if (string.IsNullOrWhiteSpace(Settings.data.backupPath))
					throw new BackupException("Backups are enabled but backupPath is not configured.");
				if (string.IsNullOrWhiteSpace(Settings.data.postgresBinPath))
					throw new BackupException("Backups are enabled but postgresBinPath is not configured.");
				if (!File.Exists(Settings.data.sevenZipCommandLineExePath))
					throw new BackupException("Backups are enabled but sevenZipCommandLineExePath is not pointing at a file.");

				FileInfo pgdumpExe = new FileInfo(Path.Combine(Settings.data.postgresBinPath, "pg_dump.exe"));
				if (!pgdumpExe.Exists)
					throw new BackupException(pgdumpExe.FullName + " file was not found. Unable to perform database backup at this time.");

				DirectoryInfo backupRoot = new DirectoryInfo(Settings.data.backupPath);
				if (!backupRoot.Exists)
					backupRoot = Directory.CreateDirectory(backupRoot.FullName);
				if (!backupRoot.Exists)
					throw new BackupException("Unable to find or create backup root directory \"" + backupRoot.FullName + "\".");

				string backupFullPath = Path.Combine(backupRoot.FullName, backupName) + ".7z";
				if (File.Exists(backupFullPath))
				{
					if (isManualBackup)
						throw new BackupException("A backup already exists at the path " + backupFullPath);
					return;
				}

				using (TempDir tmp = new TempDir("ETS_BK_TMP", true))
				{
					// 1) Backup the database to a temporary directory
					string bkTmpPath = Path.Combine(tmp.FullName, backupName + ".sql");
					string args = "--no-password --username=\"" + Database.Creation.DbCreation.dbUser + "\" --dbname=\"" + Database.Creation.DbCreation.dbName + "\" --format=plain --file=\"" + bkTmpPath + "\"";
					ProcessRunnerOptions options = new ProcessRunnerOptions();
					options.environmentVariables["PGPASSWORD"] = Settings.data.GetPostgresPassword();
					int exitCode = ProcessRunner.RunProcessAndWait(pgdumpExe.FullName, args, out string std, out string err, options);
					if (exitCode != 0)
						throw new BackupException("pg_dump returned code " + exitCode + Environment.NewLine
							+ "\tSTD:" + Environment.NewLine + std + Environment.NewLine
							+ "\tERR:" + Environment.NewLine + err);

					// 2) Compress the database copy
					string archiveTmpPath = Path.Combine(tmp.FullName, backupName + ".7z");
					SevenZip.Create7zArchive(Settings.data.sevenZipCommandLineExePath, archiveTmpPath, bkTmpPath, 1, true);

					// 3) Move the compressed archive to its permanent location
					File.Move(archiveTmpPath, backupFullPath);
				}

				if (!isManualBackup)
				{
					// 4) Prune backups
					DateTime[] allBackups = backupRoot.GetFiles("*.7z", SearchOption.TopDirectoryOnly)
					.Select(fi =>
					{
						Match m = Regex.Match(fi.Name, @"^(\d\d\d\d)-(\d\d)-(\d\d)\.7z$");
						if (m.Success)
							return new DateTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
						else
							return DateTime.MinValue;
					})
					.Where(d => d != DateTime.MinValue)
					.ToArray();

					BackupRotate rotator = new BackupRotate(TimeSpan.FromDays(1));
					DateTime[] backupsToDelete = rotator.DetermineBackupsToDelete(allBackups);
					foreach (DateTime d in backupsToDelete)
						File.Delete(Path.Combine(backupRoot.FullName, GetFilenameForDate(d)));
				}
			}
			catch (BackupException ex)
			{
				if (isManualBackup)
					throw;
				else
					Logger.Info(ex.Message);
			}
		}

		/// <summary>
		/// Restores a database backup from a 7z-compressed backup archive.
		/// </summary>
		/// <param name="fullFilePath">Path to the 7z-compressed backup archive.</param>
		/// <param name="dbAdminUser">PostgreSQL administrator username to use for initial setup.</param>
		/// <param name="dbAdminPass">PostgreSQL administrator password to use for initial setup.</param>
		/// <param name="existingDbName">Any existing PostgreSQL database name that is not the ErrorTracker database itself. "postgres" should exist by default in new installations.</param>
		public static void RestoreBackup(string fullFilePath, string dbAdminUser, string dbAdminPass, string existingDbName, Action<string> progressReport)
		{
			progressReport("Validating configuration");

			if (!File.Exists(fullFilePath))
				throw new Exception("The specified backup file could not be found: " + fullFilePath);
			if (!File.Exists(Settings.data.sevenZipCommandLineExePath))
				throw new Exception("sevenZipCommandLineExePath is not pointing at a file.");
			if (string.IsNullOrWhiteSpace(Settings.data.postgresBinPath))
				throw new Exception("postgresBinPath is not configured.");

			FileInfo psqlExe = new FileInfo(Path.Combine(Settings.data.postgresBinPath, "psql.exe"));
			if (!psqlExe.Exists)
				throw new Exception(psqlExe.FullName + " file was not found. Unable to perform database restore at this time.");

			progressReport("Validating backup archive");
			SevenZipFileData[] fileData = SevenZip.ListFiles(Settings.data.sevenZipCommandLineExePath, fullFilePath);
			string sqlFileRelativePath = fileData.FirstOrDefault(f => Path.GetFileName(f.Path).EndsWith(".sql", StringComparison.OrdinalIgnoreCase))?.Path;

			if (sqlFileRelativePath == null)
				throw new Exception(fullFilePath + " is not a recognized backup archive because it does not contain a \".sql\" file.");

			progressReport("Creating temporary directory");
			using (TempDir tmp = new TempDir("ETS_RS_TMP", true))
			{
				progressReport("Extracting backup archive");

				// Extract Backup
				SevenZip.Extract(Settings.data.sevenZipCommandLineExePath, fullFilePath, tmp.FullName);

				string sqlFilePath = Path.Combine(tmp.FullName, sqlFileRelativePath);
				if (!File.Exists(sqlFilePath))
					throw new Exception("Could not find backup sql file that should have just been extracted: " + sqlFilePath);

				progressReport("DROP existing database and role, create new");

				// Delete ErrorTracker database and recreate fresh.
				DbCreation.CreateInitialErrorTrackerDb(dbAdminUser, dbAdminPass, existingDbName);

				progressReport("Restore Backup");

				// Restore Backup
				string args = "--no-password --username=\"" + Database.Creation.DbCreation.dbUser + "\" --dbname=\"" + Database.Creation.DbCreation.dbName + "\" --file=\"" + sqlFilePath + "\"";
				ProcessRunnerOptions options = new ProcessRunnerOptions();
				options.environmentVariables["PGPASSWORD"] = Settings.data.GetPostgresPassword();
				int exitCode = ProcessRunner.RunProcessAndWait(
					psqlExe.FullName
					, args
					, e => progressReport(e.Line)
					, e => progressReport(e.Line)
					, options);
				if (exitCode != 0)
					throw new Exception("psql returned code " + exitCode);

				progressReport("Backup restoration completed without error");
			}
		}

		/// <summary>
		/// Given a DateTime, returns a string that must be used as the backup file name.
		/// </summary>
		/// <param name="d">Any DateTime value.</param>
		/// <returns></returns>
		private static string GetFilenameForDate(DateTime d)
		{
			return d.ToString("yyyy-MM-dd");
		}

	}

	[Serializable]
	internal class BackupException : Exception
	{
		public BackupException()
		{
		}

		public BackupException(string message) : base(message)
		{
		}

		public BackupException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected BackupException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}
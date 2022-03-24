using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BPUtil;

namespace ErrorTrackerServer
{
	public static class BackupManager
	{
		/// <summary>
		/// Called periodically by external code to initiate any necessary backups.
		/// </summary>
		public static void RunTasks()
		{
			if (Settings.data.backup)
			{
				// Create backups
				if (!string.IsNullOrWhiteSpace(Settings.data.backupPath))
				{
					if (File.Exists(Settings.data.sevenZipCommandLineExePath))
					{
						BackupRotate rotator = new BackupRotate(TimeSpan.FromDays(1));
						TempDir tmp = null;
						try
						{
							DirectoryInfo backupRoot = new DirectoryInfo(Settings.data.backupPath);
							if (!backupRoot.Exists)
								backupRoot = Directory.CreateDirectory(backupRoot.FullName);
							if (backupRoot.Exists)
							{
								string backupFileNameToday = GetFilenameForDate(DateTime.Now);
								foreach (Project p in Settings.data.GetAllProjects())
								{
									DirectoryInfo backupDir = new DirectoryInfo(Path.Combine(backupRoot.FullName, p.Name));
									if (!backupDir.Exists)
										backupDir = Directory.CreateDirectory(backupDir.FullName);
									if (backupDir.Exists)
									{
										string backupFullPath = Path.Combine(backupDir.FullName, backupFileNameToday);
										if (!File.Exists(backupFullPath))
										{
											// Create today's backup
											// 1) Copy this project's database to a temporary directory
											if (tmp == null)
												tmp = new TempDir("ETS_BK_TMP", true);
											string dbTmpPath;
											using (DB db = new DB(p.Name))
												dbTmpPath = db.CopyToDirectory(tmp.FullName);

											// 2) Compress the database copy
											string archiveTmpPath = Path.Combine(tmp.FullName, backupFileNameToday);
											SevenZip.Create7zArchive(Settings.data.sevenZipCommandLineExePath, archiveTmpPath, dbTmpPath, 1, true);

											// 3) Move the compressed archive to its permanent location
											File.Move(archiveTmpPath, backupFullPath);

											// 4) Prune backups
											DateTime[] allBackups = backupDir.GetFiles("*.7z", SearchOption.TopDirectoryOnly)
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

											DateTime[] backupsToDelete = rotator.DetermineBackupsToDelete(allBackups);
											foreach (DateTime d in backupsToDelete)
												File.Delete(Path.Combine(backupDir.FullName, GetFilenameForDate(d)));
										}
									}
									else
										Logger.Info("Unable to find or create project backup directory \"" + backupDir.FullName + "\".");
								}
							}
							else
								Logger.Info("Unable to find or create backup root directory \"" + backupRoot.FullName + "\".");
						}
						finally
						{
							tmp?.Dispose();
						}
					}
					else
						Logger.Info("Backups are enabled but sevenZipCommandLineExePath is not pointing at a file.");
				}
				else
					Logger.Info("Backups are enabled but backupPath is not configured.");
			}
		}
		/// <summary>
		/// Given a DateTime, returns a string that must be used as the backup file name.
		/// </summary>
		/// <param name="d">Any DateTime value.</param>
		/// <returns></returns>
		private static string GetFilenameForDate(DateTime d)
		{
			return d.ToString("yyyy-MM-dd") + ".7z";
		}
	}
}
using BPUtil;
using ErrorTrackerServer.Database.Global;
using ErrorTrackerServer.Database.Project.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ErrorTrackerServer.Code
{
	public partial class DatabaseSqliteMigrateForm : Form
	{
		BackgroundWorker bw;
		public DatabaseSqliteMigrateForm()
		{
			InitializeComponent();
		}

		private void DatabaseSqliteMigrateForm_Load(object sender, EventArgs e)
		{
			List<string> sqliteDatabases = new List<string>();
			string globalDbFilename;
			using (SQLiteMigration.Global.GlobalDb db1 = new SQLiteMigration.Global.GlobalDb())
			{
				globalDbFilename = db1.DbFilename;
				if (File.Exists(db1.DbFileFullPath))
					sqliteDatabases.Add(db1.DbFilename);
			}
			foreach (Project project in Settings.data.GetAllProjects())
			{
				using (SQLiteMigration.DB db1 = new SQLiteMigration.DB(project.Name))
				{
					if (File.Exists(db1.DbFileFullPath))
						sqliteDatabases.Add(db1.DbFilename);
				}
			}

			if (sqliteDatabases.Count > 0)
			{
				txtConsole.Text = "The following databases are ready to be migrated:" + Environment.NewLine
					+ " • " + string.Join(Environment.NewLine + " • ", sqliteDatabases) + Environment.NewLine
					+ Environment.NewLine
					+ "Before you proceed, make a backup of your \"" + globalDbFilename + "\" and \"Data/Projects/\" directory. This process will delete your SQLite databases after they are migrated!" + Environment.NewLine
					 + Environment.NewLine
					 + "When you have completed the backup, click \"Migrate\" to begin database migration." + Environment.NewLine + Environment.NewLine;

				btnOk.Enabled = true;
			}
			else
			{
				txtConsole.Text = "No SQLite databases were found.";
			}
			txtConsole.Select(0, 0);
			btnCancel.Focus();
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void btnOk_Click(object sender, EventArgs e)
		{
			btnCancel.Enabled = false;
			btnOk.Enabled = false;
			if (DialogResult.Yes == MessageBox.Show("Migration is about to begin." + Environment.NewLine + Environment.NewLine
				+ "Have you created a backup of your SQLite databases?", "SQLite Migration", MessageBoxButtons.YesNoCancel))
			{
				bw = new BackgroundWorker();
				bw.WorkerReportsProgress = true;
				bw.WorkerSupportsCancellation = false;
				bw.DoWork += Bw_DoWork;
				bw.ProgressChanged += Bw_ProgressChanged;
				bw.RunWorkerCompleted += Bw_RunWorkerCompleted;
				bw.RunWorkerAsync();
			}
			else
			{
				this.Close();
				MessageBox.Show("Database migration aborted.");
			}

		}

		private bool migrationCompleted = false;
		private void Bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (migrationCompleted)
			{
				Settings.data.postgresReady = true;
				Settings.data.Save();
				MessageBox.Show("Database migration complete. Please restart the Error Tracker service.");
			}
			else
				btnCancel.Enabled = true;
		}

		private void Bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			if (e.ProgressPercentage > -1 && progressBar1.Value != e.ProgressPercentage)
				progressBar1.SetProgressNoAnimation(e.ProgressPercentage);
			if (e.UserState != null)
			{
				txtConsole.AppendText((string)e.UserState + Environment.NewLine);
				if (txtConsole.SelectionLength == 0)
				{
					txtConsole.Select(txtConsole.TextLength, 0);
					//txtConsole.ScrollToCaret();
				}
			}
		}

		private void Bw_DoWork(object sender, DoWorkEventArgs e)
		{
			try
			{
				bw.ReportProgress(0, "===================");
				bw.ReportProgress(0, "Beginning Migration");
				bw.ReportProgress(0, "===================");
				ProgressCounter pc;
				// Migrate GlobalDb
				using (GlobalDb db2 = new GlobalDb())
				{
					db2.RunInTransaction(() =>
					{
						string dbFileFullPath = null;
						using (SQLiteMigration.Global.GlobalDb db1 = new SQLiteMigration.Global.GlobalDb())
						{
							if (File.Exists(db1.DbFileFullPath))
							{
								List<SQLiteMigration.Global.Model.LoginRecord> records = db1.GetLoginRecordsGlobal(0, 0);
								pc = new ProgressCounter(records.Count);
								bw.ReportProgress(0, "Copying GlobalDb LoginRecords (" + pc.total + ")");
								foreach (SQLiteMigration.Global.Model.LoginRecord r1 in records)
								{
									db2.AddLoginRecord(r1.UserName, r1.IPAddress, r1.SessionID, r1.Date);
									if (pc.Step())
										bw.ReportProgress(pc.progress);
								}


								dbFileFullPath = db1.DbFileFullPath;
							}
						}
						try
						{
							if (dbFileFullPath != null)
								File.Delete(dbFileFullPath);
						}
						catch (Exception ex)
						{
							throw new Exception("Unable to delete SQLite DB file \"" + dbFileFullPath + "\" because of error: " + ex.ToString());
						}

						if (dbFileFullPath != null)
							bw.ReportProgress(0, "Committing transaction");
					});
				}

				// Migrate Projects
				foreach (Project project in Settings.data.GetAllProjects())
				{
					using (DB db2 = new DB(project.Name))
					{
						db2.RunInTransaction(() =>
						{
							string dbFileFullPath = null;
							using (SQLiteMigration.DB db1 = new SQLiteMigration.DB(project.Name))
							{
								if (File.Exists(db1.DbFileFullPath))
								{
									bw.ReportProgress(0, "Copying " + project.Name + " Folders");

									SQLiteMigration.FolderStructure fs1 = db1.GetFolderStructure();
									Dictionary<int, int> folderIdMap = new Dictionary<int, int>();
									AddFolderAndChildren(fs1, folderIdMap, db2);

									Dictionary<long, long> eventIdMap = new Dictionary<long, long>();
									pc = new ProgressCounter(db1.CountEvents());
									bw.ReportProgress(0, "Copying " + project.Name + " Events (" + pc.total + ")");
									foreach (SQLiteMigration.Project.Model.Event e1 in db1.GetAllEventsNoTagsDeferred(null))
									{
										db1.GetEventTags(e1);

										if (folderIdMap.TryGetValue(e1.FolderId, out int newFolderId))
										{
											Event e2 = new Event();
											e2.Color = e1.Color;
											e2.Date = e1.Date;
											e2.EventType = (EventType)(byte)e1.EventType;
											e2.FolderId = newFolderId;
											e2.Message = e1.Message;
											e2.SubType = e1.SubType;
											foreach (SQLiteMigration.Project.Model.Tag t in e1.GetAllTags())
												e2.SetTag(t.Key, t.Value);
											e2.ComputeHash();

											db2.AddEvent(e2);
											eventIdMap[e1.EventId] = e2.EventId;
											if (pc.Step())
												bw.ReportProgress(pc.progress);
										}
										else
											throw new Exception("Failed to copy Event " + JsonConvert.SerializeObject(e1) + " because folder ID " + e1.FolderId + " was not found in the original folder structure.");
									}

									bw.ReportProgress(0, "Copying " + project.Name + " Read States");
									List<SQLiteMigration.Project.Model.ReadState> readStates = db1.GetAllReadStates();
									pc = new ProgressCounter(readStates.Count);
									foreach (SQLiteMigration.Project.Model.ReadState r1 in readStates)
									{
										if (eventIdMap.TryGetValue(r1.EventId, out long myEventId))
										{
											db2.AddReadState(r1.UserId, myEventId);
										}
										if (pc.Step())
											bw.ReportProgress(pc.progress);
									}

									bw.ReportProgress(0, "Copying " + project.Name + " Filters");
									List<SQLiteMigration.Project.Model.FullFilter> allFilters = db1.GetFilters(false).OrderBy(f => f.filter.Order).ToList();
									pc = new ProgressCounter(allFilters.Count);
									foreach (SQLiteMigration.Project.Model.FullFilter ff in allFilters)
									{
										Filter f2 = new Filter();
										f2.ConditionHandling = (ConditionHandling)(byte)ff.filter.ConditionHandling;
										f2.Enabled = ff.filter.Enabled;
										f2.MyOrder = ff.filter.Order;
										f2.Name = ff.filter.Name;

										FilterCondition[] conditions = ff.conditions
											.Select(c1 =>
											{
												FilterCondition c2 = new FilterCondition();
												c2.Enabled = c1.Enabled;
												c2.Invert = c1.Not;
												c2.Operator = (FilterConditionOperator)(byte)c1.Operator;
												c2.Query = c1.Query;
												c2.Regex = c1.Regex;
												c2.TagKey = c1.TagKey;
												return c2;
											})
											.ToArray();
										if (!db2.AddFilter(f2, conditions, out string errorMessage))
											throw new Exception("Failed to copy filter " + JsonConvert.SerializeObject(ff) + " because of error: " + errorMessage);

										foreach (SQLiteMigration.Project.Model.FilterAction a1 in ff.actions)
										{
											FilterAction a2 = new FilterAction();
											a2.Argument = a1.Argument;
											a2.Enabled = a1.Enabled;
											a2.FilterId = f2.FilterId;
											a2.Operator = (FilterActionType)(byte)a1.Operator;
											if (!db2.AddFilterAction(a2, out errorMessage))
												throw new Exception("Failed to copy filter action " + JsonConvert.SerializeObject(a1) + " into filter " + JsonConvert.SerializeObject(f2) + " because of error: " + errorMessage);
										}
										if (pc.Step())
											bw.ReportProgress(pc.progress);
									}

									dbFileFullPath = db1.DbFileFullPath;
								}
							}

							try
							{
								if (dbFileFullPath != null)
									File.Delete(dbFileFullPath);
							}
							catch (Exception ex)
							{
								throw new Exception("Unable to delete SQLite DB file \"" + dbFileFullPath + "\" because of error: " + ex.ToString());
							}

							if (dbFileFullPath != null)
								bw.ReportProgress(0, "Committing transaction");
						});
					}
				}
				bw.ReportProgress(0, "Migration completed!");
				migrationCompleted = true;
			}
			catch (Exception ex)
			{
				bw.ReportProgress(0, "Database migration failed!");
				bw.ReportProgress(0, ex.ToString());
				MessageBox.Show("Database migration failed! " + ex.FlattenMessages());
			}
		}
		private void AddFolderAndChildren(SQLiteMigration.FolderStructure src, Dictionary<int, int> folderIdMap, DB destinationDb)
		{
			if (src.Parent == null)
			{
				// This is the root folder. It must not exist in the database.
				folderIdMap[0] = 0;
			}
			else
			{
				if (!folderIdMap.TryGetValue(src.Parent.FolderId, out int newParentFolderId))
					throw new Exception("Failed to copy folder structure because folderIdMap did not contain key " + src.Parent.FolderId + " (" + src.Parent.Name + ")");
				destinationDb.AddFolder(src.Name, newParentFolderId, out string errorMessage, out Folder newFolder);
				if (!string.IsNullOrEmpty(errorMessage))
					throw new Exception("Failed to copy folder " + src.Name + " (ID " + src.FolderId + "): " + errorMessage);
				folderIdMap[src.FolderId] = newFolder.FolderId;
			}
			foreach (SQLiteMigration.FolderStructure child in src.Children)
				AddFolderAndChildren(child, folderIdMap, destinationDb);
		}
	}
	class ProgressCounter
	{
		public long completed { get; private set; } = 0;
		public readonly long total;
		public int progress { get; private set; } = -1;
		public ProgressCounter(long total)
		{
			this.total = total;
		}
		public bool Step()
		{
			completed++;
			int p = (int)Math.Floor((completed / (double)total) * 100);
			if (p != progress)
			{
				progress = p;
				return true;
			}
			return false;
		}
	}
}

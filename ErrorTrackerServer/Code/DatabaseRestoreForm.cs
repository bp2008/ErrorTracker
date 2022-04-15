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
	public partial class DatabaseRestoreForm : Form
	{
		BackgroundWorker bw;
		public DatabaseRestoreForm()
		{
			InitializeComponent();
		}

		private void DatabaseRestoreForm_Load(object sender, EventArgs e)
		{
			txtConsole.Select(txtConsole.TextLength, 0);

			DirectoryInfo backupDir = new DirectoryInfo(Settings.data.backupPath);
			if (backupDir.Exists)
			{
				foreach (FileInfo fi in backupDir.GetFiles("*.7z", SearchOption.AllDirectories))
				{
					if (fi.Extension.Equals(".7z", StringComparison.OrdinalIgnoreCase))
					{
						ListViewItem lvi = new ListViewItem(fi.Name);
						lvi.Tag = fi.FullName;
						lvBackups.Items.Add(lvi);
					}
				}

				if (lvBackups.Items.Count == 0)
					txtConsole.Text = "No backups were found at:" + Environment.NewLine + Environment.NewLine + backupDir.FullName;
			}
			else
			{
				txtConsole.Text = "Backup directory not found:" + Environment.NewLine + Environment.NewLine + backupDir.FullName;
			}
		}

		private void btnRestoreBackup_Click(object sender, EventArgs e)
		{
			if (lvBackups.Items.Count == 0 || lvBackups.SelectedItems.Count == 0)
			{
				MessageBox.Show(txtConsole.Text);
				return;
			}

			string fullFilePath = (string)lvBackups.SelectedItems[0].Tag;
			if (fullFilePath == null)
			{
				MessageBox.Show("Read the instructions, please.");
				return;
			}

			btnRestoreBackup.Enabled = false;
			txtConsole.Clear();

			bw = new BackgroundWorker();
			bw.WorkerReportsProgress = true;
			bw.DoWork += Bw_DoWork;
			bw.ProgressChanged += Bw_ProgressChanged;
			bw.RunWorkerCompleted += Bw_RunWorkerCompleted;
			bw.RunWorkerAsync(new { fullFilePath, dbAdminUser = txtAdminUser.Text, dbAdminPass = txtAdminPassword.Text, existingDbName = txtExistingDbName.Text });
		}

		private void Bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			bw = null;
			btnRestoreBackup.Enabled = true;
		}

		private void Bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
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
				dynamic args = e.Argument;
				string fullFilePath = (string)args.fullFilePath;
				string dbAdminUser = (string)args.dbAdminUser;
				string dbAdminPass = (string)args.dbAdminPass;
				string existingDbName = (string)args.existingDbName;
				BackupManager.RestoreBackup(fullFilePath, dbAdminUser, dbAdminPass, existingDbName, progress => bw.ReportProgress(0, progress));
			}
			catch (Exception ex)
			{
				bw.ReportProgress(0, ex.ToString());
			}
		}

		private void DatabaseRestoreForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (bw != null)
				e.Cancel = true;
		}
	}
}

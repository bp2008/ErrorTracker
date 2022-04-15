using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BPUtil;

namespace ErrorTrackerServer.Code
{
	public partial class DatabaseBackupForm : Form
	{
		BackgroundWorker bw;
		public DatabaseBackupForm()
		{
			InitializeComponent();
		}

		private void DatabaseBackupForm_Load(object sender, EventArgs e)
		{
			txtConsole.Text = "Backups must be configured via the admin web interface settings: backup, backupPath, postgresBinPath, sevenZipCommandLineExePath." + Environment.NewLine + Environment.NewLine
				+ "Once all of this is configured, you can test the backup system by clicking \"Backup Now\" which will create a manual full backup in the configured backup directory." + Environment.NewLine + Environment.NewLine
				+ "Manual backups are not considered by the automatic pruning system.";
			txtConsole.Select(txtConsole.TextLength, 0);
		}

		private void btnBackupNow_Click(object sender, EventArgs e)
		{
			btnBackupNow.Enabled = false;
			progressBar1.Style = ProgressBarStyle.Marquee;

			bw = new BackgroundWorker();
			bw.WorkerReportsProgress = true;
			bw.DoWork += Bw_DoWork;
			bw.RunWorkerCompleted += Bw_RunWorkerCompleted;
			bw.ProgressChanged += Bw_ProgressChanged;
			bw.RunWorkerAsync();
		}

		private void Bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			txtConsole.AppendText(Environment.NewLine + Environment.NewLine + (string)e.UserState);
			txtConsole.Select(txtConsole.TextLength, 0);
		}

		private void Bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			btnBackupNow.Enabled = true;
			progressBar1.Style = ProgressBarStyle.Blocks;
			progressBar1.SetProgressNoAnimation(100);
			bw = null;
		}

		private void DatabaseBackupForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (bw != null)
				e.Cancel = true;
		}
		private void Bw_DoWork(object sender, DoWorkEventArgs e)
		{
			try
			{
				BackupManager.BackupNow("Manual Backup at " + DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss"));
			}
			catch (Exception ex)
			{
				bw.ReportProgress(0, ex.ToString());
			}
		}
	}
}

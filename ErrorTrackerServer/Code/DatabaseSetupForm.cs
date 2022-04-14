using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ErrorTrackerServer.Code
{
	public partial class DatabaseSetupForm : BPUtil.Forms.SelfCenteredForm
	{
		public string PostgresHost { get { return txtPgsqlHost.Text; } }
		public int PostgresPort { get { return (int)nudPgsqlPort.Value; } }
		public string PostgresUser { get { return txtAdminUser.Text; } }
		public string PostgresPass { get { return txtAdminPassword.Text; } }
		public string PostgresDB { get { return txtExistingDbName.Text; } }
		public DatabaseSetupForm()
		{
			InitializeComponent();
		}
		private void DatabaseSetupForm_Load(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(Settings.data.postgresHost))
				txtPgsqlHost.Text = Settings.data.postgresHost;
			if (Settings.data.postgresPort >= 1 && Settings.data.postgresPort <= 65535)
				nudPgsqlPort.Value = Settings.data.postgresPort;
			txtAdminPassword.Focus();
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.Cancel;
			this.Close();
		}

		private void btnOK_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.OK;
			this.Close();
		}
	}
}

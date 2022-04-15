
namespace ErrorTrackerServer.Code
{
	partial class DatabaseRestoreForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DatabaseRestoreForm));
			this.label1 = new System.Windows.Forms.Label();
			this.txtConsole = new System.Windows.Forms.TextBox();
			this.progressBar1 = new System.Windows.Forms.ProgressBar();
			this.btnRestoreBackup = new System.Windows.Forms.Button();
			this.txtExistingDbName = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.txtAdminPassword = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.txtAdminUser = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.lvBackups = new System.Windows.Forms.ListView();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(94, 13);
			this.label1.TabIndex = 1;
			this.label1.Text = "Choose a backup:";
			// 
			// txtConsole
			// 
			this.txtConsole.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtConsole.Location = new System.Drawing.Point(276, 28);
			this.txtConsole.Multiline = true;
			this.txtConsole.Name = "txtConsole";
			this.txtConsole.ReadOnly = true;
			this.txtConsole.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.txtConsole.Size = new System.Drawing.Size(402, 391);
			this.txtConsole.TabIndex = 2;
			this.txtConsole.Text = resources.GetString("txtConsole.Text");
			// 
			// progressBar1
			// 
			this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.progressBar1.Location = new System.Drawing.Point(276, 425);
			this.progressBar1.MarqueeAnimationSpeed = 33;
			this.progressBar1.Name = "progressBar1";
			this.progressBar1.Size = new System.Drawing.Size(280, 23);
			this.progressBar1.TabIndex = 3;
			// 
			// btnRestoreBackup
			// 
			this.btnRestoreBackup.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnRestoreBackup.Location = new System.Drawing.Point(562, 425);
			this.btnRestoreBackup.Name = "btnRestoreBackup";
			this.btnRestoreBackup.Size = new System.Drawing.Size(116, 23);
			this.btnRestoreBackup.TabIndex = 5;
			this.btnRestoreBackup.Text = "Restore Backup";
			this.btnRestoreBackup.UseVisualStyleBackColor = true;
			this.btnRestoreBackup.Click += new System.EventHandler(this.btnRestoreBackup_Click);
			// 
			// txtExistingDbName
			// 
			this.txtExistingDbName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.txtExistingDbName.Location = new System.Drawing.Point(145, 427);
			this.txtExistingDbName.Name = "txtExistingDbName";
			this.txtExistingDbName.Size = new System.Drawing.Size(125, 20);
			this.txtExistingDbName.TabIndex = 20;
			this.txtExistingDbName.Text = "postgres";
			// 
			// label7
			// 
			this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(13, 430);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(126, 13);
			this.label7.TabIndex = 19;
			this.label7.Text = "Existing Database Name:";
			// 
			// label6
			// 
			this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label6.Location = new System.Drawing.Point(12, 388);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(258, 31);
			this.label6.TabIndex = 18;
			this.label6.Text = "Error Tracker must connect to an existing database in order to create its own dat" +
    "abase:";
			// 
			// label5
			// 
			this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label5.Location = new System.Drawing.Point(12, 285);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(258, 49);
			this.label5.TabIndex = 17;
			this.label5.Text = "Credentials for an existing admin user are required so that Error Tracker can cre" +
    "ate its own user and database:";
			// 
			// txtAdminPassword
			// 
			this.txtAdminPassword.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.txtAdminPassword.Location = new System.Drawing.Point(122, 363);
			this.txtAdminPassword.Name = "txtAdminPassword";
			this.txtAdminPassword.PasswordChar = '*';
			this.txtAdminPassword.Size = new System.Drawing.Size(148, 20);
			this.txtAdminPassword.TabIndex = 16;
			// 
			// label4
			// 
			this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(13, 366);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(104, 13);
			this.label4.TabIndex = 15;
			this.label4.Text = "Existing Admin Pass:";
			// 
			// txtAdminUser
			// 
			this.txtAdminUser.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.txtAdminUser.Location = new System.Drawing.Point(122, 337);
			this.txtAdminUser.Name = "txtAdminUser";
			this.txtAdminUser.Size = new System.Drawing.Size(148, 20);
			this.txtAdminUser.TabIndex = 14;
			this.txtAdminUser.Text = "postgres";
			// 
			// label3
			// 
			this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(13, 340);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(103, 13);
			this.label3.TabIndex = 13;
			this.label3.Text = "Existing Admin User:";
			// 
			// lvBackups
			// 
			this.lvBackups.HideSelection = false;
			this.lvBackups.Location = new System.Drawing.Point(12, 28);
			this.lvBackups.Name = "lvBackups";
			this.lvBackups.Size = new System.Drawing.Size(258, 254);
			this.lvBackups.TabIndex = 21;
			this.lvBackups.UseCompatibleStateImageBehavior = false;
			this.lvBackups.View = System.Windows.Forms.View.List;
			// 
			// DatabaseRestoreForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(690, 460);
			this.Controls.Add(this.lvBackups);
			this.Controls.Add(this.txtExistingDbName);
			this.Controls.Add(this.label7);
			this.Controls.Add(this.label6);
			this.Controls.Add(this.label5);
			this.Controls.Add(this.txtAdminPassword);
			this.Controls.Add(this.label4);
			this.Controls.Add(this.txtAdminUser);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.btnRestoreBackup);
			this.Controls.Add(this.progressBar1);
			this.Controls.Add(this.txtConsole);
			this.Controls.Add(this.label1);
			this.Name = "DatabaseRestoreForm";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "DatabaseRestoreForm";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.DatabaseRestoreForm_FormClosing);
			this.Load += new System.EventHandler(this.DatabaseRestoreForm_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox txtConsole;
		private System.Windows.Forms.ProgressBar progressBar1;
		private System.Windows.Forms.Button btnRestoreBackup;
		private System.Windows.Forms.TextBox txtExistingDbName;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.TextBox txtAdminPassword;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.TextBox txtAdminUser;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.ListView lvBackups;
	}
}
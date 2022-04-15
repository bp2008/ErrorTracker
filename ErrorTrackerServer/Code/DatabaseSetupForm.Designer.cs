
namespace ErrorTrackerServer.Code
{
	partial class DatabaseSetupForm
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
			this.label1 = new System.Windows.Forms.Label();
			this.txtPgsqlHost = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.nudPgsqlPort = new System.Windows.Forms.NumericUpDown();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.txtAdminUser = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.txtAdminPassword = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.txtExistingDbName = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.btnOK = new System.Windows.Forms.Button();
			this.btnCancel = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)(this.nudPgsqlPort)).BeginInit();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(15, 88);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(126, 13);
			this.label1.TabIndex = 0;
			this.label1.Text = "PostgreSQL Server Host:";
			// 
			// txtPgsqlHost
			// 
			this.txtPgsqlHost.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtPgsqlHost.Location = new System.Drawing.Point(147, 85);
			this.txtPgsqlHost.Name = "txtPgsqlHost";
			this.txtPgsqlHost.Size = new System.Drawing.Size(224, 20);
			this.txtPgsqlHost.TabIndex = 1;
			this.txtPgsqlHost.Text = "127.0.0.1";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(15, 114);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(147, 13);
			this.label2.TabIndex = 2;
			this.label2.Text = "PostgreSQL Server TCP Port:";
			// 
			// nudPgsqlPort
			// 
			this.nudPgsqlPort.Location = new System.Drawing.Point(168, 112);
			this.nudPgsqlPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
			this.nudPgsqlPort.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.nudPgsqlPort.Name = "nudPgsqlPort";
			this.nudPgsqlPort.Size = new System.Drawing.Size(69, 20);
			this.nudPgsqlPort.TabIndex = 3;
			this.nudPgsqlPort.Value = new decimal(new int[] {
            5432,
            0,
            0,
            0});
			// 
			// textBox1
			// 
			this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textBox1.Location = new System.Drawing.Point(12, 12);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ReadOnly = true;
			this.textBox1.Size = new System.Drawing.Size(359, 67);
			this.textBox1.TabIndex = 4;
			this.textBox1.Text = "Welcome.  Please install PostgreSQL and enter the connection information and admi" +
    "nistrative credentials for your PostgreSQL server below:";
			// 
			// txtAdminUser
			// 
			this.txtAdminUser.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtAdminUser.Location = new System.Drawing.Point(146, 188);
			this.txtAdminUser.Name = "txtAdminUser";
			this.txtAdminUser.Size = new System.Drawing.Size(225, 20);
			this.txtAdminUser.TabIndex = 6;
			this.txtAdminUser.Text = "postgres";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(15, 191);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(103, 13);
			this.label3.TabIndex = 5;
			this.label3.Text = "Existing Admin User:";
			// 
			// txtAdminPassword
			// 
			this.txtAdminPassword.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtAdminPassword.Location = new System.Drawing.Point(146, 214);
			this.txtAdminPassword.Name = "txtAdminPassword";
			this.txtAdminPassword.PasswordChar = '*';
			this.txtAdminPassword.Size = new System.Drawing.Size(225, 20);
			this.txtAdminPassword.TabIndex = 8;
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(15, 217);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(104, 13);
			this.label4.TabIndex = 7;
			this.label4.Text = "Existing Admin Pass:";
			// 
			// label5
			// 
			this.label5.Location = new System.Drawing.Point(12, 154);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(359, 31);
			this.label5.TabIndex = 9;
			this.label5.Text = "Credentials for an existing admin user are required so that Error Tracker can cre" +
    "ate its own user and database:";
			// 
			// label6
			// 
			this.label6.Location = new System.Drawing.Point(12, 253);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(359, 31);
			this.label6.TabIndex = 10;
			this.label6.Text = "Error Tracker must connect to an existing database in order to create its own dat" +
    "abase:";
			// 
			// txtExistingDbName
			// 
			this.txtExistingDbName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtExistingDbName.Location = new System.Drawing.Point(147, 287);
			this.txtExistingDbName.Name = "txtExistingDbName";
			this.txtExistingDbName.Size = new System.Drawing.Size(224, 20);
			this.txtExistingDbName.TabIndex = 12;
			this.txtExistingDbName.Text = "postgres";
			// 
			// label7
			// 
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(15, 290);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(126, 13);
			this.label7.TabIndex = 11;
			this.label7.Text = "Existing Database Name:";
			// 
			// btnOK
			// 
			this.btnOK.Location = new System.Drawing.Point(296, 318);
			this.btnOK.Name = "btnOK";
			this.btnOK.Size = new System.Drawing.Size(75, 23);
			this.btnOK.TabIndex = 14;
			this.btnOK.Text = "OK";
			this.btnOK.UseVisualStyleBackColor = true;
			this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
			// 
			// btnCancel
			// 
			this.btnCancel.Location = new System.Drawing.Point(215, 318);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(75, 23);
			this.btnCancel.TabIndex = 13;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
			// 
			// DatabaseSetupForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(385, 353);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.btnOK);
			this.Controls.Add(this.txtExistingDbName);
			this.Controls.Add(this.label7);
			this.Controls.Add(this.label6);
			this.Controls.Add(this.label5);
			this.Controls.Add(this.txtAdminPassword);
			this.Controls.Add(this.label4);
			this.Controls.Add(this.txtAdminUser);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.textBox1);
			this.Controls.Add(this.nudPgsqlPort);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.txtPgsqlHost);
			this.Controls.Add(this.label1);
			this.Name = "DatabaseSetupForm";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "PostgreSQL Database Setup Form";
			this.Load += new System.EventHandler(this.DatabaseSetupForm_Load);
			((System.ComponentModel.ISupportInitialize)(this.nudPgsqlPort)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox txtPgsqlHost;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.NumericUpDown nudPgsqlPort;
		private System.Windows.Forms.TextBox textBox1;
		private System.Windows.Forms.TextBox txtAdminUser;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox txtAdminPassword;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.TextBox txtExistingDbName;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.Button btnOK;
		private System.Windows.Forms.Button btnCancel;
	}
}
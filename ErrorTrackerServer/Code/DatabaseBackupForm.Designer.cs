
namespace ErrorTrackerServer.Code
{
	partial class DatabaseBackupForm
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
			this.txtConsole = new System.Windows.Forms.TextBox();
			this.btnBackupNow = new System.Windows.Forms.Button();
			this.progressBar1 = new System.Windows.Forms.ProgressBar();
			this.SuspendLayout();
			// 
			// txtConsole
			// 
			this.txtConsole.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtConsole.Location = new System.Drawing.Point(12, 12);
			this.txtConsole.Multiline = true;
			this.txtConsole.Name = "txtConsole";
			this.txtConsole.ReadOnly = true;
			this.txtConsole.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.txtConsole.Size = new System.Drawing.Size(476, 117);
			this.txtConsole.TabIndex = 0;
			// 
			// btnBackupNow
			// 
			this.btnBackupNow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnBackupNow.Location = new System.Drawing.Point(384, 135);
			this.btnBackupNow.Name = "btnBackupNow";
			this.btnBackupNow.Size = new System.Drawing.Size(104, 23);
			this.btnBackupNow.TabIndex = 3;
			this.btnBackupNow.Text = "Backup Now";
			this.btnBackupNow.UseVisualStyleBackColor = true;
			this.btnBackupNow.Click += new System.EventHandler(this.btnBackupNow_Click);
			// 
			// progressBar1
			// 
			this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.progressBar1.Location = new System.Drawing.Point(12, 135);
			this.progressBar1.MarqueeAnimationSpeed = 33;
			this.progressBar1.Name = "progressBar1";
			this.progressBar1.Size = new System.Drawing.Size(366, 23);
			this.progressBar1.TabIndex = 1;
			// 
			// DatabaseBackupForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(500, 170);
			this.Controls.Add(this.btnBackupNow);
			this.Controls.Add(this.progressBar1);
			this.Controls.Add(this.txtConsole);
			this.Name = "DatabaseBackupForm";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Database Backup";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.DatabaseBackupForm_FormClosing);
			this.Load += new System.EventHandler(this.DatabaseBackupForm_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox txtConsole;
		private System.Windows.Forms.Button btnBackupNow;
		private System.Windows.Forms.ProgressBar progressBar1;
	}
}
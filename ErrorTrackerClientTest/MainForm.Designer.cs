namespace ErrorTrackerClientTest
{
	partial class MainForm
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
			this.nudEventCount = new System.Windows.Forms.NumericUpDown();
			this.btnStart = new System.Windows.Forms.Button();
			this.nudSubmitThreads = new System.Windows.Forms.NumericUpDown();
			this.label2 = new System.Windows.Forms.Label();
			this.progressBar = new System.Windows.Forms.ProgressBar();
			this.lblStatus = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.txtSubmitUrl = new System.Windows.Forms.TextBox();
			((System.ComponentModel.ISupportInitialize)(this.nudEventCount)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.nudSubmitThreads)).BeginInit();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 61);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(139, 13);
			this.label1.TabIndex = 0;
			this.label1.Text = "Number of events to submit:";
			// 
			// nudEventCount
			// 
			this.nudEventCount.Location = new System.Drawing.Point(160, 59);
			this.nudEventCount.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
			this.nudEventCount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.nudEventCount.Name = "nudEventCount";
			this.nudEventCount.Size = new System.Drawing.Size(120, 20);
			this.nudEventCount.TabIndex = 1;
			this.nudEventCount.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
			// 
			// btnStart
			// 
			this.btnStart.Location = new System.Drawing.Point(160, 111);
			this.btnStart.Name = "btnStart";
			this.btnStart.Size = new System.Drawing.Size(120, 23);
			this.btnStart.TabIndex = 3;
			this.btnStart.Text = "Start Submitting";
			this.btnStart.UseVisualStyleBackColor = true;
			this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
			// 
			// nudSubmitThreads
			// 
			this.nudSubmitThreads.Location = new System.Drawing.Point(160, 85);
			this.nudSubmitThreads.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.nudSubmitThreads.Name = "nudSubmitThreads";
			this.nudSubmitThreads.Size = new System.Drawing.Size(120, 20);
			this.nudSubmitThreads.TabIndex = 2;
			this.nudSubmitThreads.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(12, 87);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(130, 13);
			this.label2.TabIndex = 3;
			this.label2.Text = "Number of submit threads:";
			// 
			// progressBar
			// 
			this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.progressBar.Location = new System.Drawing.Point(12, 140);
			this.progressBar.Name = "progressBar";
			this.progressBar.Size = new System.Drawing.Size(284, 23);
			this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
			this.progressBar.TabIndex = 5;
			// 
			// lblStatus
			// 
			this.lblStatus.AutoSize = true;
			this.lblStatus.Location = new System.Drawing.Point(12, 168);
			this.lblStatus.Name = "lblStatus";
			this.lblStatus.Size = new System.Drawing.Size(24, 13);
			this.lblStatus.TabIndex = 6;
			this.lblStatus.Text = "Idle";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(12, 9);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(88, 13);
			this.label3.TabIndex = 7;
			this.label3.Text = "Submission URL:";
			// 
			// txtSubmitUrl
			// 
			this.txtSubmitUrl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtSubmitUrl.Location = new System.Drawing.Point(15, 25);
			this.txtSubmitUrl.Name = "txtSubmitUrl";
			this.txtSubmitUrl.Size = new System.Drawing.Size(281, 20);
			this.txtSubmitUrl.TabIndex = 0;
			this.txtSubmitUrl.TextChanged += new System.EventHandler(this.txtSubmitUrl_TextChanged);
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(308, 193);
			this.Controls.Add(this.txtSubmitUrl);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.lblStatus);
			this.Controls.Add(this.progressBar);
			this.Controls.Add(this.nudSubmitThreads);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.btnStart);
			this.Controls.Add(this.nudEventCount);
			this.Controls.Add(this.label1);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Name = "MainForm";
			this.Text = "ErrorTracker Client Test";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
			((System.ComponentModel.ISupportInitialize)(this.nudEventCount)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.nudSubmitThreads)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.NumericUpDown nudEventCount;
		private System.Windows.Forms.Button btnStart;
		private System.Windows.Forms.NumericUpDown nudSubmitThreads;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.ProgressBar progressBar;
		private System.Windows.Forms.Label lblStatus;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox txtSubmitUrl;
	}
}


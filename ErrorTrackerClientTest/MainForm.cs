using ErrorTrackerClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ErrorTrackerClientTest
{
	public partial class MainForm : Form
	{
		BackgroundWorker bwSubmit;
		private int threadCount;
		private int eventCount;
		private int eventsStarted;
		private int eventsFinished;
		private string submitUrl;
		private bool closeDesired = false;
		private ErrorClient client;
		public MainForm()
		{
			InitializeComponent();
		}
		private void Reset()
		{
			bwSubmit = null;
			btnStart.Text = "Start Submitting";
			btnStart.Enabled = true;
			txtSubmitUrl.Enabled = true;
			nudEventCount.Enabled = true;
			nudSubmitThreads.Enabled = true;
			if (closeDesired)
				this.Close();
		}
		private void btnStart_Click(object sender, EventArgs e)
		{
			if (bwSubmit != null)
			{
				btnStart.Text = "Aborting…";
				btnStart.Enabled = false;
				bwSubmit.CancelAsync();
				return;
			}
			if (!Uri.TryCreate(submitUrl, UriKind.Absolute, out Uri ignored))
			{
				MessageBox.Show("Enter the Submit URL first");
				return;
			}
			if (client == null)
				client = new ErrorClient(JsonConvert.SerializeObject, () => submitUrl, () => new FileInfo(Application.ExecutablePath).Directory.FullName, true);

			btnStart.Text = "Abort";
			txtSubmitUrl.Enabled = false;
			nudEventCount.Enabled = false;
			nudSubmitThreads.Enabled = false;
			eventCount = (int)nudEventCount.Value;
			threadCount = (int)nudSubmitThreads.Value;
			eventsStarted = 0;
			eventsFinished = 0;

			bwSubmit = new BackgroundWorker();
			bwSubmit.WorkerSupportsCancellation = true;
			bwSubmit.WorkerReportsProgress = true;
			bwSubmit.DoWork += BwSubmit_DoWork;
			bwSubmit.ProgressChanged += BwSubmit_ProgressChanged;
			bwSubmit.RunWorkerCompleted += BwSubmit_RunWorkerCompleted;
			bwSubmit.RunWorkerAsync();
		}

		private void BwSubmit_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			Reset();
		}

		private void BwSubmit_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			progressBar.Value = e.ProgressPercentage;
			lblStatus.Text = e.UserState.ToString();
		}

		private void BwSubmit_DoWork(object sender, DoWorkEventArgs e)
		{
			bwSubmit.ReportProgress(0, "starting");
			List<Thread> threads = new List<Thread>(threadCount);
			for (int i = 0; i < threadCount; i++)
			{
				Thread thr = new Thread(() =>
				{
					while (Interlocked.Increment(ref eventsStarted) <= eventCount)
					{
						Event ev = BuildRandomEvent();
						client.SubmitEvent(ev);
						Interlocked.Increment(ref eventsFinished);
					}
				});
				thr.Name = "Worker " + i;
				thr.IsBackground = false;
				thr.Start();
				threads.Add(thr);
			}
			while (!bwSubmit.CancellationPending)
			{
				if (eventsFinished >= eventCount)
				{
					bwSubmit.ReportProgress(100, "finished");
					return;
				}
				else
				{
					int percent = (int)Math.Round((eventsFinished / (double)eventCount) * 100);
					bwSubmit.ReportProgress(percent, "submitting: " + percent + "%");
				}
				Thread.Sleep(16);
			}

			if (bwSubmit.CancellationPending)
				bwSubmit.ReportProgress((int)Math.Round((eventsFinished / (double)eventCount) * 100), "canceled");

			foreach (Thread thr in threads)
				thr.Abort();
			foreach (Thread thr in threads)
				thr.Join();
		}

		private void txtSubmitUrl_TextChanged(object sender, EventArgs e)
		{
			submitUrl = txtSubmitUrl.Text;
		}

		private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (bwSubmit != null)
			{
				closeDesired = true;
				bwSubmit.CancelAsync();
				e.Cancel = true;
			}
		}

		private static string[] _subTypes = new string[] { "Fish", "Colors", "Fruits" };
		private static string[][] _messages = new string[][] {
			new string[] { "Tuna", "Catfish", "Swordfish" },
			new string[] { "Red", "Green", "Blue" },
			new string[] { "Apple", "Banana", "Cherry" }
		};
		private Event BuildRandomEvent()
		{
			Event ev = new Event();
			int eventTypeIdx = StaticRandom.Next(0, 3);
			int typeIdx = StaticRandom.Next(0, 3);
			int messageIdx = StaticRandom.Next(0, 3);
			ev.EventType = (EventType)eventTypeIdx;
			ev.SubType = _subTypes[typeIdx];
			ev.Message = _messages[typeIdx][messageIdx];
			ev.Tags.Add(new Tag("Event Structure", eventTypeIdx + ":" + typeIdx + ":" + messageIdx));
			ev.Tags.Add(new Tag("Random 0-100", StaticRandom.Next(0, 101).ToString()));
			return ev;
		}
	}
}

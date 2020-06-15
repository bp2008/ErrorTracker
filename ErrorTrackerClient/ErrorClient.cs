using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerClient
{
	public class ErrorClient
	{
		private Func<string> submitUrl;
		private Func<object, string> serializeJson;
		private Func<string> pathToSaveFailedSubmissions;

		private HttpClient client;
		private HttpClientHandler httpClientHandler;
		private UTF8Encoding UTF8NoBOM = new UTF8Encoding(false);
		private Thread backgroundResendThread;

		/// <summary>
		/// <para>Initializes the ErrorClient. Create only one of these per application.</para>
		/// <para>This class maintains a background thread for retrying failed event submissions that were saved to disk.
		/// As such, only one instance should be created at the start of your app and stored in a static field to be used each time an event is to be submitted.
		/// DO NOT create additional instances of ErrorClient unless they use different [submitUrl] and [pathToSaveFailedSubmissions] arguments.</para>
		/// </summary>
		/// <param name="serializeJson">A Func which serializes an object as JSON (e.g. JsonConvert.SerializeObject)</param>
		/// <param name="submitUrl">A Func which returns the submit URL for the error tracker server.</param>
		/// <param name="pathToSaveFailedSubmissions">
		/// <para>A Func which returns a directory path which can be used to save events for later submission if realtime submission fails.</para>
		/// <para>If this path ever changes, items previously saved in it may not ever be successfully submitted to the server.</para>
		/// <para>If you use multiple Error Tracker services or multiple ErrorClient instances for any reason, this path must be unique for each one.</para>
		/// </param>
		/// <param name="acceptAnyCert">If true, this instance will accept untrusted server certificates.</param>
		public ErrorClient(Func<object, string> serializeJson, Func<string> submitUrl, Func<string> pathToSaveFailedSubmissions, bool acceptAnyCert)
		{
			this.submitUrl = submitUrl;
			this.serializeJson = serializeJson;
			this.pathToSaveFailedSubmissions = pathToSaveFailedSubmissions;

			if (!ServicePointManager.SecurityProtocol.HasFlag(SecurityProtocolType.Tls12))
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			if (ServicePointManager.DefaultConnectionLimit < 16)
				ServicePointManager.DefaultConnectionLimit = 16;

			httpClientHandler = new HttpClientHandler();
			if (acceptAnyCert)
				httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
			client = new HttpClient(httpClientHandler);
			client.DefaultRequestHeaders.ExpectContinue = false;
			client.Timeout = TimeSpan.FromSeconds(15);

			backgroundResendThread = new Thread(backgroundResendWorker);
			backgroundResendThread.Name = "Error Client Background Resend";
			backgroundResendThread.IsBackground = true;
			backgroundResendThread.Start();
		}

		/// <summary>
		/// <para>Submits the event to the server.</para>
		/// <para>If submission fails, a copy of the event will be saved to disk (if configured) and resubmitted automatically in the background until successful.</para>
		/// <para>Throws an exception if there is an I/O error trying to save an event to disk for later submission (after failing to submit the event).</para>
		/// </summary>
		/// <param name="ev"></param>
		public void SubmitEvent(Event ev)
		{
			byte[] JSON = UTF8NoBOM.GetBytes(serializeJson(ev));

			if (!SubmitSerialized(JSON))
			{
				string dirPath = pathToSaveFailedSubmissions();
				if (!string.IsNullOrWhiteSpace(dirPath))
				{
					string failurePath = Path.Combine(dirPath, TimeUtil.GetTimeInMsSinceEpoch() + "-" + Guid.NewGuid().ToString() + ".json");
					File.WriteAllBytes(failurePath, JSON);
				}
			}
		}
		/// <summary>
		/// <para>Attempts to submit the specified byte array to the server's submit URL. Returns true if successful, otherwise false.</para>
		/// <para>This method is used during original submission and also during later fallback submission.</para>
		/// </summary>
		/// <param name="JSON">JSON-serialized event data.</param>
		/// <returns></returns>
		private bool SubmitSerialized(byte[] JSON)
		{
			HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, submitUrl());
			request.Content = new ByteArrayContent(JSON);
			try
			{
				Task<HttpResponseMessage> task = Send(request);
				task.Wait();
				HttpResponseMessage httpResponse = task.Result;
				if (httpResponse.StatusCode == HttpStatusCode.OK)
					return true;
			}
			catch { }
			return false;
		}

		private async Task<HttpResponseMessage> Send(HttpRequestMessage request)
		{
			HttpResponseMessage httpResponse = await client.SendAsync(request).ConfigureAwait(false);
			return httpResponse;
		}

		private void backgroundResendWorker()
		{
			try
			{
				while (true)
				{
					int filesFound = 0;
					int filesSubmitted = 0;
					try
					{
						string dirPath = pathToSaveFailedSubmissions();
						if (!string.IsNullOrWhiteSpace(dirPath))
						{
							DirectoryInfo di = new DirectoryInfo(dirPath);
							FileInfo[] existingFiles = di.GetFiles("*.json").OrderBy(fi => fi.Name).ToArray();
							filesFound = existingFiles.Length;
							foreach (FileInfo fi in existingFiles)
							{
								byte[] data = File.ReadAllBytes(fi.FullName);
								string JSON = UTF8NoBOM.GetString(data);
								if (JSON.Contains("\"EventType\"") && JSON.Contains("\"SubType\"") && JSON.Contains("\"Message\"") && JSON.Contains("\"Date\""))
								{
									if (SubmitSerialized(data))
									{
										fi.Delete();
										filesSubmitted++;
									}
								}
							}
						}
					}
					catch { }
					if (filesFound > filesSubmitted)
						Thread.Sleep(TimeSpan.FromMinutes(5));
					else
						Thread.Sleep(TimeSpan.FromMinutes(1));
				}
			}
			catch { }
		}
	}
}

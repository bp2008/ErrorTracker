using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerClient
{
	/// <summary>
	/// Create only one of these per application.
	/// </summary>
	public class ErrorClient
	{
		private Func<object, string> serializeJson;
		private Func<string> submitUrl;
		private Func<string> pathToSaveFailedSubmissions;

		private UTF8Encoding UTF8NoBOM = new UTF8Encoding(false);
		private Thread backgroundResendThread;

		/// <summary>
		/// Gets the HttpClient in use by this class.
		/// </summary>
		public HttpClient httpClient { get; private set; }
		/// <summary>
		/// Gets the HttpClientHandler in use by this class.
		/// </summary>
		public HttpClientHandler httpClientHandler { get; private set; }
		/// <summary>
		/// Gets a value indicating if this ErrorClient will allow the server to use an untrusted certificate.
		/// </summary>
		public bool acceptAnyCertificate { get; private set; }

		/// <summary>
		/// <para>Initializes the ErrorClient. Create only one of these per application.</para>
		/// <para>This class maintains a background thread for retrying failed event submissions that were saved to disk.
		/// As such, only one instance should be created at the start of your app and stored in a static field to be used each time an event is to be submitted.
		/// DO NOT create additional instances of ErrorClient unless they use different [submitUrl] and [pathToSaveFailedSubmissions] arguments.</para>
		/// </summary>
		/// <param name="serializeJson">Provide "JsonConvert.SerializeObject" or an equivalent JSON serializing method.</param>
		/// <param name="submitUrl">A Func which returns the submit URL for the error tracker server.</param>
		/// <param name="pathToSaveFailedSubmissions">
		/// <para>A Func which returns a directory path which can be used to save events for later submission if realtime submission fails.</para>
		/// <para>If this path ever changes, items previously saved in it may not ever be successfully submitted to the server.</para>
		/// <para>If you use multiple Error Tracker services or multiple ErrorClient instances for any reason, this path must be unique for each one.</para>
		/// </param>
		/// <param name="acceptAnyCertificate">If true, this ErrorClient will allow the server to use an untrusted certificate.  Has no effect when [submitUrl] does not use "https://".</param>
		public ErrorClient(Func<object, string> serializeJson, Func<string> submitUrl, Func<string> pathToSaveFailedSubmissions, bool acceptAnyCertificate = false)
		{
			this.serializeJson = serializeJson;
			this.submitUrl = submitUrl;
			this.pathToSaveFailedSubmissions = pathToSaveFailedSubmissions;
			this.acceptAnyCertificate = acceptAnyCertificate;

			if (!ServicePointManager.SecurityProtocol.HasFlag(SecurityProtocolType.Tls12))
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			if (ServicePointManager.DefaultConnectionLimit < 16)
				ServicePointManager.DefaultConnectionLimit = 16;

			httpClientHandler = new HttpClientHandler();
			if (acceptAnyCertificate)
				httpClientHandler.ServerCertificateCustomValidationCallback = AcceptAnyCertificate;
			httpClient = new HttpClient(httpClientHandler);
			httpClient.DefaultRequestHeaders.ExpectContinue = false;
			httpClient.Timeout = TimeSpan.FromSeconds(15);

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
					FileInfo fi = new FileInfo(failurePath);
					if (!fi.Directory.Exists)
						Directory.CreateDirectory(fi.Directory.FullName);
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
			catch (Exception ex) { SubmissionFailureHandler(ex); }
			return false;
		}

		private async Task<HttpResponseMessage> Send(HttpRequestMessage request)
		{
			HttpResponseMessage httpResponse = await httpClient.SendAsync(request).ConfigureAwait(false);
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
								try
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
								catch { }
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

		private static object writeLastSubmitFailureLock = new object();
		private void SubmissionFailureHandler(Exception ex)
		{
			try
			{
				string dirPath = pathToSaveFailedSubmissions();
				if (!string.IsNullOrWhiteSpace(dirPath))
				{
					string failurePath = Path.Combine(dirPath, "LastSubmitFailure.txt");
					FileInfo fi = new FileInfo(failurePath);
					if (!fi.Directory.Exists)
						Directory.CreateDirectory(fi.Directory.FullName);
					string error = DateTime.Now.ToString() + ": " + ex.ToString();
					int tries = 4;
					lock (writeLastSubmitFailureLock)
					{
						try
						{
							File.WriteAllText(failurePath, error, UTF8NoBOM);
						}
						catch
						{
							if (--tries > 0)
								Thread.Sleep(5);
							else
								return;
						}
					}
				}
			}
			catch { }
		}
		private void DebugLog(string str)
		{
			try
			{
				string dirPath = pathToSaveFailedSubmissions();
				if (!string.IsNullOrWhiteSpace(dirPath))
				{
					string failurePath = Path.Combine(dirPath, "Debug-" + TimeUtil.GetTimeInMsSinceEpoch() + "-" + Guid.NewGuid().ToString() + ".txt");
					FileInfo fi = new FileInfo(failurePath);
					if (!fi.Directory.Exists)
						Directory.CreateDirectory(fi.Directory.FullName);
					string message = DateTime.Now.ToString() + ": " + str;
					int tries = 4;
					lock (writeLastSubmitFailureLock)
					{
						try
						{
							File.WriteAllText(failurePath, message, UTF8NoBOM);
						}
						catch
						{
							if (--tries > 0)
								Thread.Sleep(5);
							else
								return;
						}
					}
				}
			}
			catch { }
		}
		private bool AcceptAnyCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			return true;
		}
	}
}

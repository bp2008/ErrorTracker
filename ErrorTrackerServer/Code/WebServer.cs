using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using BPUtil;
using BPUtil.MVC;
using BPUtil.SimpleHttp;
using ErrorTrackerServer;
using Newtonsoft.Json;

namespace ErrorTrackerServer
{
	public class WebServer : HttpServer
	{
		private WebpackProxy webpackProxy = null;
		private MVCMain mvcMain;
		public WebServer(int port, int httpsPort, ICertificateSelector certificateSelector, IPAddress bindAddr) : base(port, httpsPort, certificateSelector, bindAddr)
		{
#if DEBUG
			if (!string.IsNullOrEmpty(Settings.data.postgresPassword))
			{
				if (Debugger.IsAttached)
					webpackProxy = new WebpackProxy(9000, Globals.ApplicationDirectoryBase + "../../");
			}
#endif

			SimpleHttpLogger.RegisterLogger(Logger.httpLogger, Settings.data.webServerVerboseLogging);
			Logger.StartLoggingThreads();

			this.XForwardedForHeader = Settings.data.useXForwardedFor;
			this.XRealIPHeader = Settings.data.useXRealIP;

			MvcJson.SerializeObject = JsonConvert.SerializeObject;
			MvcJson.DeserializeObject = JsonConvert.DeserializeObject;
			mvcMain = new MVCMain(Assembly.GetExecutingAssembly(), typeof(Controllers.Auth).Namespace, MvcErrorHandler);
		}
		public override bool shouldLogRequestsToFile()
		{
			return Settings.data.webServerRequestLogging;
		}

		private void MvcErrorHandler(RequestContext context, Exception ex)
		{
			Emailer.SendError(context, "An unhandled exception was thrown while processing an MVC request.", ex);
		}

		public override void handleGETRequest(HttpProcessor p)
		{
			if (!IsPostgreSQLDbReady())
			{
				p.writeFailure("500 Internal Server Error", GetDbReadynessString());
				return;
			}
			if (ErrorTrackerSvc.LoadingDatabases)
			{
				p.writeFailure("500 Internal Server Error", "Service is loading…");
				return;
			}

			Settings.data.RemoveAppPath(p);

			if (mvcMain.ProcessRequest(p, p.requestedPage))
			{
			}
			else
			{
				#region www
				DirectoryInfo WWWDirectory = new DirectoryInfo(Settings.data.GetWWWDirectoryBase());
				string wwwDirectoryBase = WWWDirectory.FullName.Replace('\\', '/').TrimEnd('/') + '/';

				FileInfo fi = new FileInfo(wwwDirectoryBase + p.requestedPage);
				string targetFilePath = fi.FullName.Replace('\\', '/');
				if (!targetFilePath.StartsWith(wwwDirectoryBase) || targetFilePath.Contains("../"))
				{
					p.writeFailure("400 Bad Request");
					return;
				}
				if (!p.requestedPage.Equals("service-worker.js", StringComparison.OrdinalIgnoreCase))
				{
					if (webpackProxy != null)
					{
						// Handle hot module reload provided by webpack dev server.
						switch (fi.Extension.ToLower())
						{
							case ".js":
							case ".map":
							case ".css":
							case ".json":
								webpackProxy.Proxy(p);
								return;
						}
					}
				}
				if (!fi.Exists
					|| p.requestedPage.Equals("", StringComparison.OrdinalIgnoreCase)
					|| p.requestedPage.Equals("default", StringComparison.OrdinalIgnoreCase)
					|| p.requestedPage.Equals("default.html", StringComparison.OrdinalIgnoreCase))
				{
					mvcMain.ProcessRequest(p, "Default");
					return;
				}

				if (fi.LastWriteTimeUtc.ToString("R") == p.GetHeaderValue("if-modified-since"))
				{
					p.writeSuccess(Mime.GetMimeType(fi.Extension), -1, "304 Not Modified");
					return;
				}
				using (FileStream fs = fi.OpenRead())
				{
					p.writeSuccess(Mime.GetMimeType(fi.Extension), fi.Length, additionalHeaders: GetCacheLastModifiedHeaders(TimeSpan.FromHours(1), fi.LastWriteTimeUtc));
					p.outputStream.Flush();
					fs.CopyTo(p.tcpStream);
					p.tcpStream.Flush();
				}
				#endregion
			}
		}

		private FileInfo GetDefaultFile(string wwwDirectoryBase)
		{
			return new FileInfo(wwwDirectoryBase + "Default.html");
		}
		private List<KeyValuePair<string, string>> GetCacheLastModifiedHeaders(TimeSpan maxAge, DateTime lastModifiedUTC)
		{
			List<KeyValuePair<string, string>> additionalHeaders = new List<KeyValuePair<string, string>>();
			additionalHeaders.Add(new KeyValuePair<string, string>("Cache-Control", "max-age=" + (long)maxAge.TotalSeconds + ", public"));
			additionalHeaders.Add(new KeyValuePair<string, string>("Last-Modified", lastModifiedUTC.ToString("R")));
			return additionalHeaders;
		}

		public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
		{
			if (!IsPostgreSQLDbReady())
			{
				p.writeFailure("500 Internal Server Error", GetDbReadynessString());
				return;
			}
			if (ErrorTrackerSvc.LoadingDatabases)
			{
				p.writeFailure("500 Internal Server Error", "Service is loading…");
				return;
			}

			Settings.data.RemoveAppPath(p);

			if (mvcMain.ProcessRequest(p, p.requestedPage))
			{
			}
			else
			{
			}
		}

		protected override void stopServer()
		{
		}


		/// <summary>
		/// This method must return true for the <see cref="XForwardedForHeader"/> and <see cref="XRealIPHeader"/> flags to be honored.  This method should only return true if the provided remote IP address is trusted to provide the related headers.
		/// </summary>
		/// <param name="remoteIpAddress"></param>
		/// <returns></returns>
		public override bool IsTrustedProxyServer(IPAddress remoteIpAddress)
		{
			string ip = remoteIpAddress.ToString();
			string[] trustedProxyIPs = Settings.data.trustedProxyIPs;
			if (trustedProxyIPs != null && trustedProxyIPs.Contains(ip, true))
				return true;
			return false;
		}

		private bool _dbReady = false;
		private bool IsPostgreSQLDbReady()
		{
			if (_dbReady)
				return true;
			if (string.IsNullOrEmpty(Settings.data.postgresPassword))
				return false;
			if (!Settings.data.postgresReady)
				return false;
			_dbReady = true;
			return true;
		}
		private string GetDbReadynessString()
		{
			if (IsPostgreSQLDbReady())
				return "Application error. PostgreSQL database is ready, but web server is asking for the reason it is not ready."; // This method shouldn't have been called.

			if (Settings.data.CountProjects() == 0)
				return "Error Tracker is unavailable because the PostgreSQL database has not been configured yet. Click \"Configure PostgreSQL\" in the service manager GUI and proceed to configure PostgreSQL, then restart the Error Tracker service.  No existing projects were detected, so you do not need to migrate any databases from SQLite.";
			else
			{
				if (string.IsNullOrEmpty(Settings.data.postgresPassword))
					return "Error Tracker is unavailable because the PostgreSQL database has not been configured yet. Click \"Configure PostgreSQL\" in the service manager GUI and proceed to configure PostgreSQL, then \"Migrate From SQLite\" to migrate your SQLite databases.  Then restart the Error Tracker service.";
				else
					return "Error Tracker is unavailable because you have not migrated your SQLite databases to PostgreSQL yet. Click \"Migrate From SQLite\" in the service manager GUI and proceed to migrate your SQLite databases, then restart the Error Tracker service.";
			}
		}
	}
}
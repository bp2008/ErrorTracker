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
			if (Debugger.IsAttached)
				webpackProxy = new WebpackProxy(9000, Globals.ApplicationDirectoryBase + "../../");
#endif
			MvcJson.SerializeObject = JsonConvert.SerializeObject;
			MvcJson.DeserializeObject = JsonConvert.DeserializeObject;
			mvcMain = new MVCMain(Assembly.GetExecutingAssembly(), typeof(Controllers.Auth).Namespace, ex => Logger.Debug(ex));
		}

		public override void handleGETRequest(HttpProcessor p)
		{
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
	}
}
using BPUtil;
using BPUtil.MVC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Controllers
{
	public class SettingsData : AdminController
	{
		public ActionResult GetSettingsData()
		{
			GetSettingsResponse response = new GetSettingsResponse();
			return Json(response);
		}
		public ActionResult SetSettingsData()
		{
			SetSettingsRequest request = ApiRequestBase.ParseRequest<SetSettingsRequest>(this);

			bool requiresRestart = false;
			Settings.data.systemName = request.settings.systemName;
			if (Settings.data.port_http != request.settings.port_http)
			{
				Settings.data.port_http = request.settings.port_http;
				requiresRestart = true;
			}
			if (Settings.data.port_https != request.settings.port_https)
			{
				Settings.data.port_https = request.settings.port_https;
				requiresRestart = true;
			}
			Settings.data.appPath = request.settings.appPath;
			if (Settings.data.certificatePath != request.settings.certificatePath)
			{
				Settings.data.certificatePath = request.settings.certificatePath;
				requiresRestart = true;
			}
			if (Settings.data.certificatePassword != request.settings.certificatePassword)
			{
				Settings.data.certificatePassword = request.settings.certificatePassword;
				requiresRestart = true;
			}
			Settings.data.loginStyle = request.settings.loginStyle.ToString();
			Settings.data.geolocationWebServiceBaseUrl = request.settings.geolocationWebServiceBaseUrl.ToString();
			Settings.data.trustedProxyIPs = request.settings.trustedProxyIPs;
			Settings.data.useXRealIP = request.settings.useXRealIP;
			Settings.data.useXForwardedFor = request.settings.useXForwardedFor;
			Settings.data.smtpHost = request.settings.smtpHost;
			Settings.data.smtpPort = request.settings.smtpPort;
			Settings.data.smtpSsl = request.settings.smtpSsl;
			Settings.data.smtpUser = request.settings.smtpUser;
			Settings.data.smtpPass = request.settings.smtpPass;
			Settings.data.smtpSendFrom = request.settings.smtpSendFrom;
			Settings.data.defaultErrorEmail = request.settings.defaultErrorEmail;
			Settings.data.verboseSubmitLogging = request.settings.verboseSubmitLogging;
			Settings.data.webServerVerboseLogging = request.settings.webServerVerboseLogging;
			Settings.data.webServerRequestLogging = request.settings.webServerRequestLogging;
			Settings.data.verboseSubmitLogging = request.settings.verboseSubmitLogging;
			Settings.data.serviceWorkerEnabled = request.settings.serviceWorkerEnabled;
			Settings.data.backup = request.settings.backup;
			Settings.data.backupPath = request.settings.backupPath;
			Settings.data.postgresBinPath = request.settings.postgresBinPath;
			Settings.data.sevenZipCommandLineExePath = request.settings.sevenZipCommandLineExePath;
			Settings.data.postgresCommandTimeout = request.settings.postgresCommandTimeout;
			Settings.data.Save();

			BPUtil.SimpleHttp.SimpleHttpLogger.RegisterLogger(Logger.httpLogger, Settings.data.webServerVerboseLogging);

			SetSettingsResponse response = new SetSettingsResponse(true, null);
			if (requiresRestart)
				response.message = "Some changes will take effect the next time the web server is restarted.";
			return Json(response);
		}
		public ActionResult RestartServer()
		{
			Thread thrRestartSelf = new Thread(() =>
			{
				try
				{
					Thread.Sleep(1000);

					string restartBat = Globals.WritableDirectoryBase + "RestartService.bat";
					File.WriteAllText(restartBat, "NET STOP \"" + ErrorTrackerSvc.SvcName + "\"" + Environment.NewLine + "NET START \"" + ErrorTrackerSvc.SvcName + "\"");

					ProcessStartInfo psi = new ProcessStartInfo(restartBat, "");
					psi.UseShellExecute = true;
					psi.CreateNoWindow = true;
					Process.Start(psi);
				}
				catch (Exception ex)
				{
					Logger.Debug(ex);
				}
			});
			thrRestartSelf.Name = "Restart Self";
			thrRestartSelf.IsBackground = true;
			thrRestartSelf.Start();

			return Json(new ApiResponseBase(true));
		}
	}
	public class GetSettingsResponse : ApiResponseBase
	{
		/// <summary>
		/// An object containing the current settings.
		/// </summary>
		public SettingsObject settings;
		/// <summary>
		/// Contains metadata about all settings data fields for the purpose of creating a field-editing GUI.
		/// </summary>
		public List<FieldEditSpec> editSpec;
		public GetSettingsResponse() : base(true, null)
		{
			settings = new SettingsObject(Settings.data);
			SettingsObject soDefault = new SettingsObject(new Settings());
			editSpec = soDefault.GetType().GetFields().Select(f =>
			{
				return new FieldEditSpec(f, soDefault);
			}).ToList();
		}
	}
	public class SetSettingsRequest : ApiRequestBase
	{
		/// <summary>
		/// An object containing the new settings.
		/// </summary>
		public SettingsObject settings;
	}
	public class SetSettingsResponse : ApiResponseBase
	{
		/// <summary>
		/// Message to show to the user.  May be null.
		/// </summary>
		public string message;

		public SetSettingsResponse(bool success, string error = null) : base(success, error)
		{
		}
	}

	public class SettingsObject
	{
		[HelpMd("A short display name for your system, used mainly in page titles.")]
		public string systemName;
		[HelpMd("The TCP port number for the web server to listen on for unsecured HTTP.")]
		public int port_http;
		[HelpMd("The TCP port number for the web server to listen on for TLS-secured HTTPS.")]
		public int port_https;
		[HelpMd("The path you are hosting the server at. Default: `/`. If you are using a reverse proxy server that listens on a virtual directory, (e.g. `https://example.com/EventTracker/`), then set the virtual directory path here (e.g. `/EventTracker/`).")]
		public string appPath;
		[HelpMd("The embedded web server automatically generates a self-signed certificate. If you wish to use a different certificate, you can set the path to a \".pfx\" file here.")]
		public string certificatePath;
		[HelpMd("If you've chosen a `.pfx` file that requires a password, set the password here.")]
		public string certificatePassword;
		[HelpMd("The login page has a few styles to choose from.  The default style (`wallpaper`) loads a different image every day from bing.com.")]
		[JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
		public LoginStyle loginStyle;
		[HelpMd("If you have a [GeolocationWebService](https://github.com/bp2008/GeolocationWebService) instance you'd like to use with ErrorTracker, you can set the URL of that server here.  (e.g. `http://127.0.0.1:52280/`).  ErrorTracker will use the geolocation web service to show location data when you click items in \"Login History\" tables.  To simplify firewall rules and configuration, all requests to the geolocation web service are made by the ErrorTrackerService process, never by the client's web browser.")]
		public string geolocationWebServiceBaseUrl;
		[HelpMd("If you are using a reverse proxy server, you may want to pass real client IP addresses to the ErrorTracker service for logging purposes.  ErrorTracker supports learning real client IPs from the `X-Real-IP` or `X-Forwarded-For` headers, but first you must add the IP address of your reverse proxy server(s) here.")]
		public string[] trustedProxyIPs = new string[0];
		[HelpMd("If checked, ErrorTracker will read the `X-Real-IP` header if the request comes from a trusted reverse proxy server IP address.  **Requires server restart for changes to take effect.**")]
		public bool useXRealIP = false;
		[HelpMd("If checked, ErrorTracker will read the `X-Forwarded-For` header if the request comes from a trusted reverse proxy server IP address.  **Requires server restart for changes to take effect.**")]
		public bool useXForwardedFor = false;
		[HelpMd("SMTP host name or IP address to send mail through. Required for email-based password reset functionality and internal error reporting features to be available.")]
		public string smtpHost = "";
		[HelpMd("Port number for the SMTP connection.")]
		public int smtpPort = 587;
		[HelpMd("Whether or not to use SSL for the SMTP connection.")]
		public bool smtpSsl = true;
		[HelpMd("User name to use when authenticating with the SMTP server. (optional)")]
		public string smtpUser = "";
		[HelpMd("Password to use when authenticating with the SMTP server. (optional)")]
		public string smtpPass = "";
		[HelpMd("Email address to send mail from.")]
		public string smtpSendFrom = "";
		[HelpMd("Email address or addresses, separated by semicolons or commas, to send error emails to.  This is for Error Tracker to notify the administrator of certain errors.")]
		public string defaultErrorEmail = "";
		[HelpMd("If checked, all event submissions will be logged to a file for debugging purposes.")]
		public bool verboseSubmitLogging = false;
		[HelpMd("If checked, additional web server information may be logged.")]
		public bool webServerVerboseLogging = false;
		[HelpMd("If true, incoming web requests will be logged to a file.")]
		public bool webServerRequestLogging = false;
		[HelpMd("If checked, ErrorTracker will provide a service worker to connected clients to handle PUSH notifications.  PUSH notifications require the service worker to be enabled, and also require an HTTPS connection and a web browser that implements Web Push.")]
		public bool serviceWorkerEnabled = false;
		[HelpMd("If checked, backups of all the databases will be saved in the configured backup folder shortly after midnight each morning.  Backups are automatically deleted as they age.")]
		public bool backup = false;
		[HelpMd("Folder path to save backups in.")]
		public string backupPath = "";
		[HelpMd("Path to the PostgreSQL bin folder, needed for backup and restore functionality.  On Windows this is typically `C:\\Program Files\\PostgreSQL\\14\\bin`")]
		public string postgresBinPath = "";
		[HelpMd("Path to 7za executable. Required for database backups.")]
		public string sevenZipCommandLineExePath = "";
		[HelpMd("Number of seconds to wait for SQL commands to complete before canceling them. If 0, the timeout is disabled and commands can run forever. Min: 0, Max: 86400. Requires service restart to take effect.")]
		public int postgresCommandTimeout = 900;
		public SettingsObject() { }
		public SettingsObject(Settings settings)
		{
			systemName = settings.systemName;
			port_http = settings.port_http;
			port_https = settings.port_https;
			appPath = settings.appPath;
			certificatePath = settings.certificatePath;
			certificatePassword = settings.certificatePassword;
			Enum.TryParse(settings.loginStyle, out loginStyle);
			geolocationWebServiceBaseUrl = settings.geolocationWebServiceBaseUrl;
			trustedProxyIPs = settings.trustedProxyIPs;
			useXRealIP = settings.useXRealIP;
			useXForwardedFor = settings.useXForwardedFor;
			smtpHost = settings.smtpHost;
			smtpPort = settings.smtpPort;
			smtpSsl = settings.smtpSsl;
			smtpUser = settings.smtpUser;
			smtpPass = settings.smtpPass;
			smtpSendFrom = settings.smtpSendFrom;
			defaultErrorEmail = settings.defaultErrorEmail;
			verboseSubmitLogging = settings.verboseSubmitLogging;
			webServerVerboseLogging = settings.webServerVerboseLogging;
			webServerRequestLogging = settings.webServerRequestLogging;
			serviceWorkerEnabled = settings.serviceWorkerEnabled;
			backup = settings.backup;
			backupPath = settings.backupPath;
			postgresBinPath = settings.postgresBinPath;
			sevenZipCommandLineExePath = settings.sevenZipCommandLineExePath;
			postgresCommandTimeout = settings.postgresCommandTimeout;
		}
	}
	public enum LoginStyle
	{
		style1,
		style2,
		wallpaper
	}
}

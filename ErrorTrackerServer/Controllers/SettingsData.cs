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
			Settings.data.Save();

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
		public string systemName;
		public int port_http;
		public int port_https;
		public string appPath;
		public string certificatePath;
		public string certificatePassword;
		[JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
		public LoginStyle loginStyle;
		public string geolocationWebServiceBaseUrl;
		public string[] trustedProxyIPs = new string[0];
		public bool useXRealIP = false;
		public bool useXForwardedFor = false;
		public string smtpHost = "";
		public int smtpPort = 587;
		public bool smtpSsl = true;
		public string smtpUser = "";
		public string smtpPass = "";
		public string smtpSendFrom = "";
		public string defaultErrorEmail = "";
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
		}
	}
	public enum LoginStyle
	{
		style1,
		style2,
		wallpaper
	}
}

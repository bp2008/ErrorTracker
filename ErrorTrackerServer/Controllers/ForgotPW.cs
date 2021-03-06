﻿using BPUtil;
using BPUtil.MVC;
using BPUtil.PasswordReset;
using ErrorTrackerServer.Database.Project.Model;
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
	public class ForgotPW : ETController
	{
		private const double minutesBetweenRequestsByName = 5; // 5 minutes between requests by user name
		private static ObjectCache<string, bool> requestLimiterByUsername = new ObjectCache<string, bool>(2000000, minutesBetweenRequestsByName); 
		private const double minutesBetweenRequestsByIp = 0.25; // 15 seconds between requests by IP
		private static ObjectCache<string, bool> requestLimiterByIP = new ObjectCache<string, bool>(2000000, minutesBetweenRequestsByIp); 
		private static ObjectCache<string, bool> resetLimiterByIP = new ObjectCache<string, bool>(2000000, minutesBetweenRequestsByIp);
		public ActionResult Available()
		{
			return Json(new AvailabilityResponse(Emailer.Enabled));
		}
		public ActionResult InitiateRequest()
		{
			if (!Emailer.Enabled)
				return ApiError("This server does not have email configured. Therefore, this function is not usable.");

			if (requestLimiterByIP.Get(Context.httpProcessor.RemoteIPAddressStr))
				return ApiError("A password reset request was recently initiated from " + Context.httpProcessor.RemoteIPAddressStr + ". Rate-limiting is in effect. Please wait " + TimeSpan.FromMinutes(minutesBetweenRequestsByIp).TotalSeconds + " seconds between requests.");
			requestLimiterByIP.Add(Context.httpProcessor.RemoteIPAddressStr, true);

			ForgotPasswordRequest request = ApiRequestBase.ParseRequest<ForgotPasswordRequest>(this);
			ErrorTrackerPasswordReset resetter = new ErrorTrackerPasswordReset();
			PasswordResetRequest req = resetter.GetResetRequest(request.accountIdentifier);
			if (req != null)
			{
				if (requestLimiterByUsername.Get(req.accountIdentifier))
					return ApiError("A password reset request was recently received for this user. Rate-limiting is in effect. Please wait " + TimeSpan.FromMinutes(minutesBetweenRequestsByName).TotalMinutes + " minutes between requests.");
				requestLimiterByUsername.Add(req.accountIdentifier, true);
				StringBuilder sb = new StringBuilder();
				sb.Append("Hello ");
				sb.Append(req.displayName);
				sb.Append(",\r\n\r\n");
				sb.Append("Someone at the address \"" + Context.httpProcessor.RemoteIPAddressStr
					+ "\" has requested a reset of your password at \"" + Settings.data.systemName
					+ "\". If you did not make this request, you can ignore this message and your password will not be changed.\r\n\r\n");
				sb.Append("Here is your Security Code:\r\n\r\n");
				sb.Append("-----------------------------\r\n");
				sb.Append(req.secureToken);
				sb.Append("\r\n-----------------------------");
				sb.Append("\r\n\r\nCopy it to the \"Password Recovery\" page and a new password will be emailed to you.  This code expires in ");
				sb.Append((int)req.tokenExpiration.TotalMinutes);
				sb.Append(" minutes.\r\n\r\n(This email is automated.  Please do not reply.)");
				Emailer.SendEmail(req.email, Settings.data.systemName + " Password Recovery", sb.ToString(), false);
			}
			return Json(new ApiResponseBase(true));
		}
		public ActionResult Reset()
		{
			if (!Emailer.Enabled)
				return ApiError("This server does not have email configured. Therefore, this function is not usable.");

			if (resetLimiterByIP.Get(Context.httpProcessor.RemoteIPAddressStr))
				return ApiError("A password reset request was recently attempted from " + Context.httpProcessor.RemoteIPAddressStr + ". Rate-limiting is in effect. Please wait " + TimeSpan.FromMinutes(minutesBetweenRequestsByIp).TotalSeconds + " seconds between requests.");
			resetLimiterByIP.Add(Context.httpProcessor.RemoteIPAddressStr, true);

			ForgotPasswordRequest request = ApiRequestBase.ParseRequest<ForgotPasswordRequest>(this);
			ErrorTrackerPasswordReset resetter = new ErrorTrackerPasswordReset();
			string newPassword = resetter.CompletePasswordReset(resetter.accountType, request.accountIdentifier, request.token.Trim(), out PasswordResetRequest req);
			if (newPassword == null)
				return ApiError("Unable to reset password. Your Security Code may be invalid or expired.");

			StringBuilder sb = new StringBuilder();
			sb.Append("Hello ");
			sb.Append(req.displayName);
			sb.Append(",\r\n\r\n");
			sb.Append("Your password at \"" + Settings.data.systemName + "\" has been set to:\r\n\r\n");
			sb.Append("-----------------------------\r\n");
			sb.Append(newPassword);
			sb.Append("\r\n-----------------------------");
			sb.Append("\r\n\r\n(This email is automated.  Please do not reply.)");
			Emailer.SendEmail(req.email, Settings.data.systemName + " New Password", sb.ToString(), false);

			return Json(new ApiResponseBase(true));
		}
	}
	public class ForgotPasswordRequest : ApiRequestBase
	{
		/// <summary>
		/// User Name
		/// </summary>
		public string accountIdentifier;
		/// <summary>
		/// Security token generated by us and provided via email.
		/// </summary>
		public string token;
	}
	public class AvailabilityResponse : ApiResponseBase
	{
		public bool available;

		public AvailabilityResponse(bool available) : base(true)
		{
			this.available = available;
		}
	}
}

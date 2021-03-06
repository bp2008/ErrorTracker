﻿using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Database.Global;
using ErrorTrackerServer.Database.Global.Model;
using ErrorTrackerServer.Filtering;
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
	public class LoginRecordData : UserController
	{
		private static WebRequestUtility proxyClient = new WebRequestUtility("ErrorTrackerServer", 5000);
		/// <summary>
		/// Gets login records for the past 2 months for the session's logged-in user, ordered by Date descending.  Does not require admin privilege.
		/// </summary>
		/// <returns></returns>
		public ActionResult GetLoginRecordsForSelf()
		{
			long startDate = TimeUtil.GetTimeInMsSinceEpoch(DateTime.Now.AddMonths(-2));
			long endDate = TimeUtil.GetTimeInMsSinceEpoch(DateTime.Now.AddMonths(1));
			using (GlobalDb db = new GlobalDb())
				return Json(new LoginRecordsResponse() { records = db.GetLoginRecordsByUserName(session.userName, startDate, endDate) });
		}
		/// <summary>
		/// Gets all login records for the specified user, ordered by Date descending.  Session must have admin privilege.
		/// </summary>
		/// <returns></returns>
		public ActionResult GetLoginRecordsForUser()
		{
			if (!session.IsAdminValid)
				return ApiError("Not Authorized");

			LoginRecordsByUserNameRequest request = ApiRequestBase.ParseRequest<LoginRecordsByUserNameRequest>(this);

			using (GlobalDb db = new GlobalDb())
				return Json(new LoginRecordsResponse() { records = db.GetLoginRecordsByUserName(request.userName) });
		}
		/// <summary>
		/// Gets all login records within a time range, ordered by Date descending.  Session must have admin privilege.
		/// </summary>
		/// <returns></returns>
		public ActionResult GetLoginRecordsGlobal()
		{
			if (!session.IsAdminValid)
				return ApiError("Not Authorized");

			LoginRecordsGlobalRequest request = ApiRequestBase.ParseRequest<LoginRecordsGlobalRequest>(this);

			using (GlobalDb db = new GlobalDb())
				return Json(new LoginRecordsResponse() { records = db.GetLoginRecordsGlobal(request.startDate, request.endDate) });
		}
		/// <summary>
		/// Gets all login records within a time range, ordered by Date descending.  Session must have admin privilege.
		/// </summary>
		/// <returns></returns>
		public ActionResult GeolocateIP()
		{
			GeolocateIPRequest request = ApiRequestBase.ParseRequest<GeolocateIPRequest>(this);

			if (IPAddress.TryParse(request.ip, out IPAddress ip))
			{

				string url = Settings.data.geolocationWebServiceBaseUrl;
				if (string.IsNullOrWhiteSpace(url))
					return ApiError("The geolocation web service endpoint is not configured.");
				if (!url.EndsWith("/"))
					url += "/";
				url += "embed/" + ip.ToString();
				BpWebResponse proxyResponse = proxyClient.GET(url);
				return Json(new GeolocateIPResponse() { html = proxyResponse.str });
			}
			else
				return ApiError("Invalid IP Address");
		}
	}
	public class LoginRecordsResponse : ApiResponseBase
	{
		public List<LoginRecord> records;
		public LoginRecordsResponse() : base(true, null) { }
	}
	public class LoginRecordsByUserNameRequest : ApiRequestBase
	{
		/// <summary>
		/// User name to return login records for.
		/// </summary>
		public string userName;
	}
	public class LoginRecordsGlobalRequest : ApiRequestBase
	{
		/// <summary>
		/// Oldest allowable date to return login records for.
		/// </summary>
		public long startDate;
		/// <summary>
		/// Newest allowable date to return login records for.
		/// </summary>
		public long endDate;
	}
	public class GeolocateIPRequest : ApiRequestBase
	{
		/// <summary>
		/// IP Address to geolocate.
		/// </summary>
		public string ip;
	}
	public class GeolocateIPResponse : ApiResponseBase
	{
		public string html;
		public GeolocateIPResponse() : base(true, null) { }
	}
}

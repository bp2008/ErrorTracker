using BPUtil;
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
	public class LoginRecordsGlobalRequest : ProjectRequestBase
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
}

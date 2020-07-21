using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Database.Model;
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
	public class SearchData : UserController
	{
		public ActionResult SearchSimple()
		{
			SearchSimpleRequest request = ApiRequestBase.ParseRequest<SearchSimpleRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			SearchResultsResponse response = new SearchResultsResponse();
			using (FilterEngine fe = new FilterEngine(p.Name))
			{
				response.events = fe.Search(request.query, request.folderId);
				foreach (Event e in response.events)
					e.ClearTags();
			}
			return Json(response);
		}
		public ActionResult SearchAdvanced()
		{
			SearchAdvancedRequest request = ApiRequestBase.ParseRequest<SearchAdvancedRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			SearchResultsResponse response = new SearchResultsResponse();
			using (FilterEngine fe = new FilterEngine(p.Name))
			{
				response.events = fe.AdvancedSearch(request.conditions, request.matchAll, request.folderId);
				foreach (Event e in response.events)
					e.ClearTags();
			}
			return Json(response);
		}
	}
	public class SearchResultsResponse : ApiResponseBase
	{
		public List<Event> events;
		public SearchResultsResponse() : base(true, null) { }
	}
	public class SearchSimpleRequest : ProjectRequestBase
	{
		/// <summary>
		/// Simple search query that matches against any of the event's Tag values, Message, EventType, or SubType. Not case-sensitive.
		/// </summary>
		public string query;
		/// <summary>
		/// Folder ID to search.  If -1, all events are searched.
		/// </summary>
		public int folderId = -1;
	}
	public class SearchAdvancedRequest : ProjectRequestBase
	{
		/// <summary>
		/// Array of conditions that must be met.
		/// </summary>
		public FilterCondition[] conditions;
		/// <summary>
		/// If true, all conditions must be met.  If false, only one condition needs to be met.
		/// </summary>
		public bool matchAll = false;
		/// <summary>
		/// Folder ID to search.  If -1, all events are searched.
		/// </summary>
		public int folderId = -1;
	}
}

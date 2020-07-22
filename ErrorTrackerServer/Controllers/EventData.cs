using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Database;
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
	public class EventData : UserController
	{
		/// <summary>
		/// Gets all events (without their tags) within the specified date range.
		/// </summary>
		/// <returns></returns>
		public ActionResult GetEventsByDate()
		{
			GetEventsByDateRequest request = ApiRequestBase.ParseRequest<GetEventsByDateRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			GetEventSummaryResponse response = new GetEventSummaryResponse();
			using (DB db = new DB(p.Name))
			{
				response.events = db.GetEventsWithoutTagsByDate(request.startTime, request.endTime)
					.Select(ev => new EventSummary(ev))
					.ToList();
			}
			return Json(response);
		}
		/// <summary>
		/// Gets events (without their tags), optionally filtering by Folder Id and/or date range.
		/// </summary>
		/// <returns></returns>
		public ActionResult GetEvents()
		{
			GetEventsRequest request = ApiRequestBase.ParseRequest<GetEventsRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			GetEventSummaryResponse response = new GetEventSummaryResponse();
			using (DB db = new DB(p.Name))
			{
				List<Event> events;
				if (request.startTime == 0 && request.endTime == 0)
				{
					if (request.folderId < 0)
						events = db.GetAllEventsNoTags();
					else
						events = db.GetEventsWithoutTagsInFolder(request.folderId);
				}
				else
				{
					if (request.folderId < 0)
						events = db.GetEventsWithoutTagsByDate(request.startTime, request.endTime);
					else
						events = db.GetEventsWithoutTagsInFolderByDate(request.folderId, request.startTime, request.endTime);
				}
				response.events = events
					.Select(ev => new EventSummary(ev))
					.ToList();
			}
			return Json(response);
		}
		/// <summary>
		/// Gets a specific event by ID.
		/// </summary>
		/// <returns></returns>
		public ActionResult GetEvent()
		{
			GetEventRequest request = ApiRequestBase.ParseRequest<GetEventRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			Event ev = null;
			using (DB db = new DB(p.Name))
				ev = db.GetEvent(request.eventId);
			if (ev != null)
			{
				GetEventDataResponse response = new GetEventDataResponse();
				response.ev = ev;
				return Json(response);
			}
			return ApiError("Unable to find event with ID " + request.eventId);
		}
		/// <summary>
		/// Move events by ID to a new folder by ID.
		/// </summary>
		/// <returns></returns>
		public ActionResult MoveEvents()
		{
			MoveEventsRequest request = ApiRequestBase.ParseRequest<MoveEventsRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.MoveEvents(request.eventIds, request.newFolderId))
					return Json(new ApiResponseBase(true));
				return ApiError("Unable to move all events with IDs " + string.Join(",", request.eventIds));
			}
		}
		/// <summary>
		/// Deletes events by ID.
		/// </summary>
		/// <returns></returns>
		public ActionResult DeleteEvents()
		{
			EventIdsRequest request = ApiRequestBase.ParseRequest<EventIdsRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.DeleteEvents(request.eventIds))
					return Json(new ApiResponseBase(true));
				return ApiError("Unable to delete all events with IDs " + string.Join(",", request.eventIds));
			}
		}
		/// <summary>
		/// Deletes events by ID.
		/// </summary>
		/// <returns></returns>
		public ActionResult SetEventsColor()
		{
			SetEventsColorRequest request = ApiRequestBase.ParseRequest<SetEventsColorRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.SetEventsColor(request.eventIds, request.color))
					return Json(new ApiResponseBase(true));
				return ApiError("Unable to delete all events with IDs " + string.Join(",", request.eventIds));
			}
		}
	}

	/// <summary>
	/// A subset of event fields including a truncated Message suitable for display in a list.
	/// </summary>
	public class EventSummary
	{
		public long EventId;
		public string EventType;
		public string SubType;
		public long Date;
		public string Message;
		[JsonConverter(typeof(HexStringJsonConverter), 3)]
		public uint Color;
		public EventSummary(Event ev)
		{
			EventId = ev.EventId;
			EventType = ev.EventType.ToString();
			SubType = ev.SubType;
			Date = ev.Date;
			Message = ev.Message;
			if (Message != null && Message.Length > 150)
				Message = Message.Substring(0, 150);
			Color = ev.Color;
		}
	}

	public class GetEventsByDateRequest : ProjectRequestBase
	{
		public long startTime;
		public long endTime;
	}
	public class GetEventsRequest : GetEventsByDateRequest
	{
		public int folderId;
	}

	public class GetEventSummaryResponse : ApiResponseBase
	{
		/// <summary>
		/// A list containing the requested events.
		/// </summary>
		public List<EventSummary> events;
		public GetEventSummaryResponse() : base(true, null) { }
	}

	public class GetEventDataResponse : ApiResponseBase
	{
		/// <summary>
		/// The requested event.
		/// </summary>
		public Event ev;
		public GetEventDataResponse() : base(true, null) { }
	}
	public class GetEventRequest : ProjectRequestBase
	{
		public long eventId;
	}
	public class MoveEventRequest : GetEventRequest
	{
		public int newFolderId;
	}
	public class EventIdsRequest : ProjectRequestBase
	{
		public long[] eventIds;
	}
	public class MoveEventsRequest : EventIdsRequest
	{
		public int newFolderId;
	}
	public class SetEventsColorRequest : EventIdsRequest
	{
		[JsonConverter(typeof(HexStringJsonConverter), 3)]
		public uint color;
	}
}

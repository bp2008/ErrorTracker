using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Database;
using ErrorTrackerServer.Database.Project.Model;
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
	public class EventData : UserController
	{
		/// <summary>
		/// Gets events (without their tags), optionally filtering by Folder Id and/or date range.
		/// </summary>
		/// <returns></returns>
		public ActionResult GetEvents()
		{
			GetEventsRequest request = ApiRequestBase.ParseRequest<GetEventsRequest>(this);

			if (!request.Validate(false, out Project p, out ApiResponseBase error))
				return Json(error);

			string customTagKey = session.GetUser().GetEventListCustomTagKey(p.Name);

			GetEventSummaryResponse response = new GetEventSummaryResponse();
			using (DB db = new DB(p.Name))
			{
				List<Event> events;
				if (request.startTime == 0 && request.endTime == 0)
				{
					if (request.folderId < 0)
						events = db.GetAllEventsNoTags(customTagKey);
					else
						events = db.GetEventsWithoutTagsInFolder(request.folderId, customTagKey);
				}
				else
				{
					if (request.folderId < 0)
						events = db.GetEventsWithoutTagsByDate(request.startTime, request.endTime, customTagKey);
					else
						events = db.GetEventsWithoutTagsInFolderByDate(request.folderId, request.startTime, request.endTime, customTagKey);
				}

				if (request.uniqueOnly)
				{
					List<Event> unique = new List<Event>();
					HashSet<string> hashesAdded = new HashSet<string>();
					foreach (Event e in events.OrderByDescending(e => e.Date))
					{
						if (!hashesAdded.Contains(e.HashValue))
						{
							hashesAdded.Add(e.HashValue);
							unique.Add(e);
						}
					}
					events = unique;
				}

				HashSet<long> readEventIds = new HashSet<long>(db.GetAllReadEventIds(session.GetUser().UserId));

				response.events = events
					.Select(ev => new EventSummary(ev, readEventIds))
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

			if (!request.Validate(false, out Project p, out ApiResponseBase error))
				return Json(error);

			Event ev = null;
			using (DB db = new DB(p.Name))
			{
				ev = db.GetEvent(request.eventId);
				if (ev != null)
					db.AddReadState(session.GetUser().UserId, ev.EventId);
			}
			if (ev != null)
			{
				GetEventDataResponse response = new GetEventDataResponse();
				response.ev = ev;
				response.eventListCustomTagKey = session.GetUser().GetEventListCustomTagKey(p.Name);
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

			if (!request.Validate(true, out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.MoveEvents(request.eventIds, request.newFolderId))
					return Json(new ApiResponseBase(true));
				return ApiError("Unable to move all events with IDs " + string.Join(",", request.eventIds));
			}
		}
		/// <summary>
		/// Move events by ID to new folders by ID. Each event can be moved to a different folder.
		/// </summary>
		/// <returns></returns>
		public ActionResult MoveEventsMap()
		{
			MoveEventsMapRequest request = ApiRequestBase.ParseRequest<MoveEventsMapRequest>(this);

			if (!request.Validate(true, out Project p, out ApiResponseBase error))
				return Json(error);

			DeferredActionCollection dac = new DeferredActionCollection();
			foreach (KeyValuePair<long, int> kvp in request.eventIdToNewFolderId)
				dac.MoveEventTo(kvp.Key, kvp.Value);
			using (DB db = new DB(p.Name))
			{
				if (dac.ExecuteDeferredActions(db))
					return Json(new ApiResponseBase(true));
				return ApiError("Unable to move all events with IDs " + string.Join(",", request.eventIdToNewFolderId.Keys));
			}
		}
		/// <summary>
		/// Deletes events by ID.
		/// </summary>
		/// <returns></returns>
		public ActionResult DeleteEvents()
		{
			EventIdsRequest request = ApiRequestBase.ParseRequest<EventIdsRequest>(this);

			if (!request.Validate(true, out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.DeleteEvents(request.eventIds))
					return Json(new ApiResponseBase(true));
				return ApiError("Unable to delete all events with IDs " + string.Join(",", request.eventIds));
			}
		}
		/// <summary>
		/// Sets the color of events by ID.
		/// </summary>
		/// <returns></returns>
		public ActionResult SetEventsColor()
		{
			SetEventsColorRequest request = ApiRequestBase.ParseRequest<SetEventsColorRequest>(this);

			if (!request.Validate(true, out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.SetEventsColor(request.eventIds, request.color))
					return Json(new ApiResponseBase(true));
				return ApiError("Unable to delete all events with IDs " + string.Join(",", request.eventIds));
			}
		}
		/// <summary>
		/// Sets the ReadState of events by ID.
		/// </summary>
		/// <returns></returns>
		public ActionResult SetEventsReadState()
		{
			SetEventsReadStateRequest request = ApiRequestBase.ParseRequest<SetEventsReadStateRequest>(this);

			if (!request.Validate(false, out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (request.read)
					db.AddReadState(session.GetUser().UserId, request.eventIds);
				else
					db.RemoveReadState(session.GetUser().UserId, request.eventIds);
				return Json(new ApiResponseBase(true));
			}
		}
		/// <summary>
		/// Sets the ReadState of events by ID.
		/// </summary>
		/// <returns></returns>
		public ActionResult SetFolderReadState()
		{
			SetFolderReadStateRequest request = ApiRequestBase.ParseRequest<SetFolderReadStateRequest>(this);

			if (!request.Validate(false, out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				long unreadEvents = db.SetFolderReadState(session.GetUser().UserId, request.folderId, request.read);
				return Json(new SetFolderReadStateResponse(unreadEvents));
			}
		}
		/// <summary>
		/// Gets the number of unread events in every folder that contains unread events.
		/// </summary>
		/// <returns></returns>
		public ActionResult CountUnreadEventsByFolder()
		{
			ProjectRequestBase request = ApiRequestBase.ParseRequest<ProjectRequestBase>(this);

			if (!request.Validate(false, out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				Dictionary<int, uint> folderIdToUnreadEventCount = db.CountUnreadEventsByFolder(session.GetUser().UserId);
				return Json(new CountUnreadEventsByFolderResponse(folderIdToUnreadEventCount));
			}
		}
		/// <summary>
		/// Sets the custom tag to be included in event summaries provided to this user. Set null to unset the preference.
		/// </summary>
		/// <returns></returns>
		public ActionResult SetEventListCustomTagKey()
		{
			SetEventListCustomTagKeyRequest request = ApiRequestBase.ParseRequest<SetEventListCustomTagKeyRequest>(this);

			if (!request.Validate(false, out Project p, out ApiResponseBase error))
				return Json(error);

			session.GetUser().SetEventListCustomTagKey(p.Name, request.eventListCustomTagKey);
			Settings.data.Save();

			return Json(new ApiResponseBase(true));
		}
		/// <summary>
		/// Copies the events with the given IDs and submits them to a different project.
		/// </summary>
		/// <returns></returns>
		public ActionResult CopyEventsToProject()
		{
			CopyEventsToProjectRequest request = ApiRequestBase.ParseRequest<CopyEventsToProjectRequest>(this);

			if (!request.Validate(false, out Project pSrc, out ApiResponseBase error))
				return Json(error);

			if (!ProjectRequestBase.Validate(request.targetProjectName, request.GetSession(), true, out Project pDest, out ApiResponseBase error2))
				return Json(error2);

			using (DB dbSrc = new DB(pSrc.Name))
			{
				List<long> fatalErrorEventIds = new List<long>();
				List<long> filterErrorEventIds = new List<long>();
				foreach (int eventIdSrc in request.eventIds)
				{
					Event ev = dbSrc.GetEvent(eventIdSrc);
					SubmitResult result = Submit.InsertIntoProject(Context, pDest, ev);
					if (result == SubmitResult.FatalError)
						fatalErrorEventIds.Add(eventIdSrc);
					else if (result == SubmitResult.FilterError)
						filterErrorEventIds.Add(eventIdSrc);
				}
				if (fatalErrorEventIds.Count == 0 && filterErrorEventIds.Count == 0)
					return Json(new ApiResponseBase(true));
				else
					return Json(new CopyEventsToProjectFailResponse(fatalErrorEventIds, filterErrorEventIds));
			}
		}
	}

	/// <summary>
	/// A subset of event fields including a truncated Message suitable for display in a list.
	/// </summary>
	public class EventSummary
	{
		public long EventId;
		public int FolderId;
		public string EventType;
		public string SubType;
		public long Date;
		public string Message;
		[JsonConverter(typeof(HexStringJsonConverter), 3)]
		public uint Color;
		/// <summary>
		/// True if the requesting user has read this event already.
		/// </summary>
		public bool Read;
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string CTag;

		public EventSummary(Event ev, HashSet<long> readEventIds)
		{
			EventId = ev.EventId;
			FolderId = ev.FolderId;
			EventType = ev.EventType.ToString();
			SubType = ev.SubType;
			Date = ev.Date;
			Message = ev.Message;
			if (Message != null && Message.Length > 150)
				Message = Message.Substring(0, 150);
			Color = ev.Color;
			Read = readEventIds.Contains(ev.EventId);
			if (ev is EventWithCustomTagValue)
				CTag = ((EventWithCustomTagValue)ev).CTag;
		}
	}

	public class GetEventsRequest : ProjectRequestBase
	{
		public long startTime;
		public long endTime;
		public int folderId;
		/// <summary>
		/// If true, only the most recent one of each event sharing a HashValue will be returned.
		/// </summary>
		public bool uniqueOnly;
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
		/// <summary>
		/// Key of custom tag chosen by this user to appear in EventNode components.
		/// </summary>
		public string eventListCustomTagKey;
		public GetEventDataResponse() : base(true, null) { }
	}
	public class GetEventRequest : ProjectRequestBase
	{
		public long eventId;
	}
	public class EventIdsRequest : ProjectRequestBase
	{
		public long[] eventIds;
	}
	public class MoveEventsRequest : EventIdsRequest
	{
		public int newFolderId;
	}
	public class CopyEventsToProjectRequest : EventIdsRequest
	{
		public string targetProjectName;
	}
	public class CopyEventsToProjectFailResponse : ApiResponseBase
	{
		/// <summary>
		/// Event IDs that failed to be copied to the target project.
		/// </summary>
		public long[] fatalErroredEventIds;
		/// <summary>
		/// Event IDs that failed to be filtered by the target project.
		/// </summary>
		public long[] filterErroredEventIds;
		public CopyEventsToProjectFailResponse(IEnumerable<long> fatalErroredEventIds, IEnumerable<long> filterErroredEventIds)
			: base(false, "Failed to copy events [" + string.Join(", ", fatalErroredEventIds)
				  + "]. Failed to filter events [" + string.Join(", ", filterErroredEventIds) + "].")
		{
			this.fatalErroredEventIds = fatalErroredEventIds.ToArray();
			this.filterErroredEventIds = filterErroredEventIds.ToArray();
		}
	}
	public class MoveEventsMapRequest : ProjectRequestBase
	{
		public Dictionary<long, int> eventIdToNewFolderId;
	}
	public class SetEventsColorRequest : EventIdsRequest
	{
		[JsonConverter(typeof(HexStringJsonConverter), 3)]
		public uint color;
	}
	public class SetEventsReadStateRequest : EventIdsRequest
	{
		public bool read;
	}
	public class SetFolderReadStateRequest : ProjectRequestBase
	{
		public int folderId;
		public bool read;
	}
	public class SetFolderReadStateResponse : ApiResponseBase
	{
		public long unreadEventCount;
		public SetFolderReadStateResponse(long unreadEventCount) : base(true, null)
		{
			this.unreadEventCount = unreadEventCount;
		}
	}
	public class CountUnreadEventsByFolderResponse : ApiResponseBase
	{
		/// <summary>
		/// A map of folderId to unread event count. Folders with no unread events may not be in the map.
		/// </summary>
		public Dictionary<int, uint> folderIdToUnreadEventCount;
		public CountUnreadEventsByFolderResponse(Dictionary<int, uint> folderIdToUnreadEventCount) : base(true, null)
		{
			this.folderIdToUnreadEventCount = folderIdToUnreadEventCount;
		}
	}
	public class SetEventListCustomTagKeyRequest : ProjectRequestBase
	{
		public string eventListCustomTagKey;
	}
}

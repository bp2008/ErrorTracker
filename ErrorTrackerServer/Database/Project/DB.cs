using BPUtil;
using ErrorTrackerServer.Controllers;
using ErrorTrackerServer.Database.Creation;
using ErrorTrackerServer.Database.Project.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	/// <summary>
	/// Use within a "using" block to guarantee correct disposal.  Provides SQLite database access.  Not thread safe.  
	/// </summary>
	public class DB : DBBase
	{
		#region Constructor / Fields
		/// <summary>
		/// Project name.
		/// </summary>
		public readonly string ProjectName;
		/// <summary>
		/// Lower-case project name.
		/// </summary>
		public readonly string ProjectNameLower;
		/// <summary>
		/// Collection for keeping track of which database files have been initialized during this run of the app.  This could be run every time, but it is only necessary to do it once per app instance.
		/// </summary>
		private static ConcurrentDictionary<string, bool> initializedDatabases = new ConcurrentDictionary<string, bool>();
		private static object createMigrateLock = new object();
		///// <summary>
		///// Collection for keeping locks for each projectName.
		///// </summary>
		private static ConcurrentDictionary<string, object> dbTransactionLocks = new ConcurrentDictionary<string, object>();
		/// <summary>
		/// Use within a "using" block to guarantee correct disposal.  Provides SQL database access.  Not thread safe.
		/// </summary>
		/// <param name="projectName">Project name, case insensitive. Please validate the project name before passing it in, as this class will create the database if it doesn't exist.</param>
		public DB(string projectName)
		{
			ProjectName = projectName;
			ProjectNameLower = projectName.ToLower();
			CreateOrMigrate();
		}

		private void CreateOrMigrate()
		{
			bool initialized;
			if (!initializedDatabases.TryGetValue(ProjectNameLower, out initialized) || !initialized)
			{
				lock (createMigrateLock) // It is a minor (insignificant) inefficiency having a single lock for all database initialization.
				{
					if (!initializedDatabases.TryGetValue(ProjectNameLower, out initialized) || !initialized)
					{
						DbCreation.CreateOrMigrateProject(ProjectNameLower);

						initializedDatabases[ProjectNameLower] = true;
					}
				}
			}
		}
		#endregion

		#region Helpers
		protected override object GetTransactionLock()
		{
			return dbTransactionLocks.GetOrAdd(this.ProjectName, n => new object());
		}
		protected override string GetSchemaName()
		{
			return ProjectNameLower;
		}
		protected override string GetConnectionString()
		{
			return DbCreation.GetConnectionString();
		}
		#endregion

		#region Event Management
		/// <summary>
		/// Preprocesses an event before adding or updating it in the database in order to ensure certain constraints are not violated.
		/// </summary>
		/// <param name="e"></param>
		private void PreprocessEvent(Event e)
		{
			if (e.Message == null)
				e.Message = "[Message was null]";
			if (e.SubType == null)
				e.SubType = "[SubType was null]";
			if (e.HashValue == null)
				e.ComputeHash();
		}
		/// <summary>
		/// Adds the specified event to the database.
		/// </summary>
		/// <param name="e">Event to add.  Any Tags attached to this event are also added.</param>
		public void AddEvent(Event e)
		{
			PreprocessEvent(e);
			RunInTransaction(() =>
			{
				Insert(e);
				IEnumerable<Tag> tags = e.GetAllTags();
				InsertAll(tags);
			});
		}
		///// <summary>
		///// Moves the specified event to the specified folder, returning true if successful.
		///// </summary>
		///// <param name="eventId">Event ID to move.</param>
		///// <param name="newFolderId">Folder ID to move the event into.</param>
		///// <returns></returns>
		//public bool MoveEvent(long eventId, int newFolderId)
		//{
		//	if (GetFolder(newFolderId) == null)
		//		return false;
		//	if (Execute<int>("UPDATE Event SET FolderId = ? WHERE EventId = ?", newFolderId, eventId) == 1)
		//		return true;
		//	return false;
		//}
		/// <summary>
		/// Moves the specified events to the specified folder, returning true if all of the events were moved. (false if the number of affected rows != eventIds.Length)
		/// </summary>
		/// <param name="eventIds">Event IDs to move.</param>
		/// <param name="newFolderId">Folder ID to move the events into.</param>
		/// <returns></returns>
		public bool MoveEvents(long[] eventIds, int newFolderId)
		{
			int affectedRows = 0;
			RunInTransaction(() =>
			{
				if (newFolderId == 0 || GetFolder(newFolderId) != null)
					affectedRows = ExecuteNonQuery("UPDATE " + ProjectNameLower + ".Event SET FolderId = " + newFolderId + " WHERE EventId IN (" + string.Join(",", eventIds) + ")");
			});
			if (affectedRows == eventIds.Length)
				return true;
			return false;
		}
		///// <summary>
		///// Deletes the specified event, returning true if successful.
		///// </summary>
		///// <param name="eventId">Event ID to delete.</param>
		///// <returns></returns>
		//public bool DeleteEvent(long eventId)
		//{
		//	if (Delete<Event>(eventId) == 1)
		//		return true;
		//	return false;
		//}
		/// <summary>
		/// Deletes the specified events and their Tags, returning true if all of the events were deleted. (false if the number of affected Event rows != eventIds.Length)
		/// </summary>
		/// <param name="eventId">Event IDs to delete.</param>
		/// <returns></returns>
		public bool DeleteEvents(long[] eventIds)
		{
			if (eventIds.Length == 0)
				return true;
			int affectedRows = 0;
			RunInTransaction(() =>
			{
				affectedRows = ExecuteNonQuery("DELETE FROM " + ProjectNameLower + ".Event WHERE EventId IN (" + string.Join(",", eventIds) + ")");
				ExecuteNonQuery("DELETE FROM " + ProjectNameLower + ".Tag WHERE EventId IN (" + string.Join(",", eventIds) + ")");
				ExecuteNonQuery("DELETE FROM " + ProjectNameLower + ".ReadState WHERE EventId IN (" + string.Join(",", eventIds) + ")");
			});
			if (affectedRows == eventIds.Length)
				return true;
			return false;
		}
		/// <summary>
		/// Sets the color of the specified events, returning true if all of the events were updated. (false if the number of affected rows != eventIds.Length)
		/// </summary>
		/// <param name="eventIds">Event IDs to color.</param>
		/// <param name="color">ARGB Color to set, where 0xFFFF0000 is red, 0xFF00FF00 is green, 0xFF0000FF is blue.  The alpha channel may be ignored by client apps.</param>
		/// <returns></returns>
		public bool SetEventsColor(long[] eventIds, uint color)
		{
			int affectedRows = RunInTransaction(() =>
			{
				return ExecuteNonQuery("UPDATE " + ProjectNameLower + ".Event SET Color = " + color + " WHERE EventId IN (" + string.Join(",", eventIds) + ")");
			});
			if (affectedRows == eventIds.Length)
				return true;
			return false;
		}
		/// <summary>
		/// Adds the specified events to the database.
		/// </summary>
		/// <param name="e">Events to add.  Any Tags attached to these events are also added.</param>
		public void AddEvents(ICollection<Event> events)
		{
			if (events.Count > 0)
			{
				foreach (Event e in events)
					PreprocessEvent(e);
				RunInTransaction(() =>
				{
					InsertAll(events);
					List<Tag> allTags = new List<Tag>();
					foreach (Event e in events)
					{
						IEnumerable<Tag> tags = e.GetAllTags();
						allTags.AddRange(tags);
					}
					if (allTags.Count > 0)
						InsertAll(allTags);
				});
			}
		}
		/// <summary>
		/// Gets the event with the specified ID, or null if the event is not found.
		/// </summary>
		/// <param name="eventId"></param>
		/// <returns></returns>
		public Event GetEvent(long eventId)
		{
			List<Event> events = null;
			List<Tag> tags = null;
			RunInTransaction(() =>
			{
				events = Query<Event>("EventId", eventId);
				if (events.Count > 0)
				{
					tags = ExecuteQuery<Tag>("SELECT * FROM " + ProjectNameLower + ".Tag WHERE EventId = " + eventId).ToList();
					events[0].MatchingEvents = ExecuteScalar<long>("SELECT COUNT(EventId) FROM " + ProjectNameLower + ".Event WHERE HashValue = @hasharg", new { hasharg = events[0].HashValue });
				}
			});
			if (events.Count > 0)
			{
				AddTagsToEvents(events, tags);
				return events[0];
			}
			return null;
		}
		/// <summary>
		/// Returns a collection that iterates through all the Events. If you need to use an Event's tags, you will need to call <see cref="GetEventTags"/> on the Event.
		/// </summary>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <returns></returns>
		public List<Event> GetAllEventsNoTags(string eventListCustomTagKey)
		{
			return GetEvents(null, null, null, eventListCustomTagKey, includeTags: false, deferred: false).ToList();
			//if (eventListCustomTagKey == null)
			//	return QueryAll<Event>();
			//else
			//	return ExecuteQuery<EventWithCustomTagValue>(
			//		"SELECT e.*, t.Value as CTag FROM " + ProjectNameLower + ".Event e "
			//		+ " LEFT JOIN " + ProjectNameLower + ".Tag t ON e.EventId = t.EventId AND t.Key = @customtagkey", new { customtagkey = eventListCustomTagKey })
			//		.Select(e => (Event)e)
			//		.ToList();
		}
		/// <summary>
		/// Returns a collection that iterates through all the Events without needing to load them all into memory first. If you need to use an Event's tags, you will need to call <see cref="GetEventTags"/> on the Event.
		/// </summary>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <returns></returns>
		public IEnumerable<Event> GetAllEventsDeferred(string eventListCustomTagKey)
		{
			return GetEvents(null, null, null, eventListCustomTagKey, includeTags: true, deferred: true).ToList();
		}
		///// <summary>
		///// Loads the event's tags from the database only if the event currently has no tags defined. Meant to be used with deferred event getters such as <see cref="GetAllEventsNoTagsDeferred"/>.
		///// </summary>
		///// <param name="ev">The event to load tags into.</param>
		///// <returns></returns>
		//public void GetEventTags(Event ev)
		//{
		//	if (ev.GetTagCount() > 0 || ev.EventId < 1)
		//		return;
		//	IEnumerable<Tag> tags = ExecuteQuery<Tag>("SELECT * FROM " + ProjectNameLower + ".Tag WHERE EventId = " + ev.EventId);
		//	foreach (Tag t in tags)
		//		ev.SetTag(t.Key, t.Value);
		//}
		/// <summary>
		/// Gets all events within the specified date range.
		/// </summary>
		/// <param name="oldestEpoch">Start date in milliseconds since the unix epoch.</param>
		/// <param name="newestEpoch">End date in milliseconds since the unix epoch.</param>
		/// <returns></returns>
		public List<Event> GetEventsByDate(long oldestEpoch, long newestEpoch)
		{
			return GetEvents(oldestEpoch, newestEpoch, null, null, includeTags: true, deferred: false).ToList();
		}
		/// <summary>
		/// Gets multiple events from the DB using a variety of arguments.
		/// </summary>
		/// <param name="oldest">Oldest date of the range (inclusive).</param>
		/// <param name="newest">Newest date of the range (inclusive).</param>
		/// <param name="folderid">Folder ID (if null or negative, all folders will be included)</param>
		/// <param name="customtagkey">Key of a particular tag to include as "CTag" field.</param>
		/// <param name="includeTags">If true, each event will be prepopulated with tags.</param>
		/// <param name="deferred">If true, the request will be ongoing while you iterate over the returned value.
		/// Prevents large data sets from consuming as much memory.
		/// If false, the returned value is a List and the request is over before the method completes.</param>
		/// <returns></returns>
		private IEnumerable<Event> GetEvents(long? oldest = null, long? newest = null, int? folderid = null, string customtagkey = null, bool includeTags = false, bool deferred = false)
		{
			List<string> selections = new List<string>();
			selections.Add("e.*");
			if (!string.IsNullOrWhiteSpace(customtagkey))
				selections.Add("t.Value AS CTag");
			if (includeTags)
				selections.Add("(SELECT jsonb_object_agg(t.Key, t.Value) FROM " + ProjectNameLower + ".tag t WHERE t.EventId = e.EventId ) AS TagsJson");

			PetaPoco.Sql request = PetaPoco.Sql.Builder
				.Select(string.Join(", ", selections))
				.From(ProjectNameLower + ".Event e");

			if (!string.IsNullOrWhiteSpace(customtagkey))
				request.LeftJoin(ProjectNameLower + ".Tag t").On("e.EventId = t.EventId AND t.Key = @0", customtagkey);
			if (oldest != null && newest != null)
				request.Where("e.Date >= @0", oldest).Where("e.Date <= @0", newest);
			if (folderid != null && folderid > -1)
				request.Where("e.FolderId = @0", folderid);

			if (deferred)
			{
				if (includeTags)
				{
					if (!string.IsNullOrWhiteSpace(customtagkey))
						return DeferredExecuteQuery<EventWithTagsAndCustomTagValue>(request);
					else
						return DeferredExecuteQuery<EventWithTags>(request);
				}
				else
				{
					if (!string.IsNullOrWhiteSpace(customtagkey))
						return DeferredExecuteQuery<EventWithCustomTagValue>(request);
					else
						return DeferredExecuteQuery<Event>(request);
				}
			}
			else
			{
				if (includeTags)
				{
					if (!string.IsNullOrWhiteSpace(customtagkey))
						return ExecuteQuery<EventWithTagsAndCustomTagValue>(request);
					else
						return ExecuteQuery<EventWithTags>(request);
				}
				else
				{
					if (!string.IsNullOrWhiteSpace(customtagkey))
						return ExecuteQuery<EventWithCustomTagValue>(request);
					else
						return ExecuteQuery<Event>(request);
				}
			}
		}
		/// <summary>
		/// Gets all events within the specified date range. Does not populate the Tags field.
		/// </summary>
		/// <param name="oldestEpoch">Start date in milliseconds since the unix epoch.</param>
		/// <param name="newestEpoch">End date in milliseconds since the unix epoch.</param>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <returns></returns>
		public List<Event> GetEventsWithoutTagsByDate(long oldestEpoch, long newestEpoch, string eventListCustomTagKey)
		{
			return GetEvents(oldestEpoch, newestEpoch, null, eventListCustomTagKey, includeTags: true, deferred: false).ToList();
		}
		/// <summary>
		/// Gets all events from the specified folder.
		/// </summary>
		/// <param name="folderId">Folder ID.</param>
		/// <returns></returns>
		public List<Event> GetEventsInFolder(int folderId)
		{
			List<Event> events = null;
			List<Tag> tags = null;
			RunInTransaction(() =>
			{
				events = GetEventsWithoutTagsInFolder(folderId, null);
				if (events.Count > 0)
					tags = ExecuteQuery<Tag>("SELECT t.* FROM " + ProjectNameLower + ".Tag t INNER JOIN " + ProjectNameLower + ".Event e ON t.EventId = e.EventId WHERE e.FolderId = " + folderId).ToList();
			});
			if (events.Count > 0)
				AddTagsToEvents(events, tags);
			return events;
		}

		/// <summary>
		/// Gets all events from the specified folder. Does not populate the Tags field.
		/// </summary>
		/// <param name="folderId">Folder ID. Negative ID matches All Folders. 0 matches root.</param>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <returns></returns>
		public List<Event> GetEventsWithoutTagsInFolder(int folderId, string eventListCustomTagKey)
		{
			return GetEvents(null, null, folderId < 0 ? (int?)null : folderId, eventListCustomTagKey, includeTags: false, deferred: false).ToList();
		}

		/// <summary>
		/// Gets all events from the specified folder. Does not populate the Tags field. Does not load all events into memory first.
		/// </summary>
		/// <param name="folderId">Folder ID. Negative ID matches All Folders.</param>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <returns></returns>
		public IEnumerable<Event> GetEventsInFolderDeferred(int folderId, string eventListCustomTagKey)
		{
			return GetEvents(null, null, folderId < 0 ? (int?)null : folderId, eventListCustomTagKey, includeTags: true, deferred: true);
		}

		/// <summary>
		/// Gets all events from the specified folder within the specified date range. Does not populate the Tags field.
		/// </summary>
		/// <param name="folderId">Folder ID. Negative ID matches All Folders.</param>
		/// <param name="oldestEpoch">Start date in milliseconds since the unix epoch.</param>
		/// <param name="newestEpoch">End date in milliseconds since the unix epoch.</param>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <returns></returns>
		public List<Event> GetEventsWithoutTagsInFolderByDate(int folderId, long oldestEpoch, long newestEpoch, string eventListCustomTagKey)
		{
			return GetEvents(oldestEpoch, newestEpoch, folderId < 0 ? (int?)null : folderId, eventListCustomTagKey, includeTags: false, deferred: false).ToList();
		}
		/// <summary>
		/// Given a list of Event and a list of Tag, adds each tag to the appropriate event.
		/// </summary>
		/// <param name="events">List of events.</param>
		/// <param name="tags">List of tags.</param>
		private void AddTagsToEvents(List<Event> events, List<Tag> tags)
		{
			Dictionary<long, Event> eventDict = new Dictionary<long, Event>(events.Count);
			foreach (Event e in events)
				eventDict[e.EventId] = e;

			foreach (Tag t in tags)
				if (eventDict.TryGetValue(t.EventId, out Event e))
					e.SetTag(t.Key, t.Value);
		}
		/// <summary>
		/// Returns the number of events in the folder. Non-existent folders contain 0 events.
		/// </summary>
		/// <param name="folderId">ID of the folder.</param>
		/// <returns></returns>
		public long CountEventsInFolder(int folderId)
		{
			return ExecuteScalar<long>("SELECT COUNT(*) FROM " + ProjectNameLower + ".Event WHERE FolderId = @folderId", new { folderId });
		}
		/// <summary>
		/// Returns the number of total events in each folder that has events.
		/// </summary>
		/// <returns></returns>
		public Dictionary<int, uint> CountEventsByFolder()
		{
			List<EventsInFolderCount> counts = ExecuteQuery<EventsInFolderCount>("SELECT FolderId, COUNT(EventId) as Count "
				+ " FROM " + ProjectNameLower + ".Event"
				+ " GROUP BY FolderId").ToList();

			return ConvertToDictionary(counts);
		}
		/// <summary>
		/// Returns the number of unread events in each folder that has unread events.
		/// </summary>
		/// <param name="userId">ID of the user.</param>
		/// <returns></returns>
		public Dictionary<int, uint> CountUnreadEventsByFolder(int userId)
		{
			// First, get the count of events by folder
			Dictionary<int, uint> all = null;
			Dictionary<int, uint> read = null;
			RunInTransaction(() =>
			{
				all = CountEventsByFolder();
				read = CountReadEventsByFolder(userId);
			});
			int[] folders = all.Keys.ToArray();
			foreach (int folderId in folders)
			{
				if (read.TryGetValue(folderId, out uint r))
					all[folderId] = all[folderId] - r;
			}
			return all;
		}
		/// <summary>
		/// Returns the number of read events in each folder that has read events.
		/// </summary>
		/// <param name="userId">ID of the user.</param>
		/// <returns></returns>
		public Dictionary<int, uint> CountReadEventsByFolder(int userId)
		{
			List<EventsInFolderCount> counts = ExecuteQuery<EventsInFolderCount>("SELECT FolderId, COUNT(e.EventId) as Count "
				+ " FROM " + ProjectNameLower + ".Event e"
				+ " INNER JOIN " + ProjectNameLower + ".ReadState rs ON e.EventId = rs.EventId"
				+ " WHERE rs.UserId = " + userId
				+ " GROUP BY FolderId").ToList();

			return ConvertToDictionary(counts);
		}
		private Dictionary<int, uint> ConvertToDictionary(List<EventsInFolderCount> counts)
		{
			return counts.ToDictionary(c => c.FolderId, c => c.Count);
		}
		/// <summary>
		/// Returns true if the event with the specified ID exists.
		/// </summary>
		/// <param name="eventId">Event ID to look up.</param>
		/// <returns></returns>
		public bool EventExists(int eventId)
		{
			return ExecuteScalar<int>("SELECT COUNT(*) FROM " + ProjectNameLower + ".Event WHERE EventId = " + eventId) > 0;
		}
		/// <summary>
		/// Deletes all events with Date lower than the specified value. Returns the number of events that were deleted.
		/// </summary>
		/// <param name="ageCutoff">Events with Date lower than this will be deleted.</param>
		public int DeleteEventsOlderThan(long ageCutoff)
		{
			int eventCount = 0;
			RunInTransaction(() =>
			{
				long[] events = ExecuteQuery<long>("SELECT EventId FROM " + ProjectNameLower + ".Event WHERE Date < @age", new { age = ageCutoff }).ToArray();
				eventCount = events.Length;
				if (!DeleteEvents(events))
					throw new Exception("Unable to delete all " + eventCount + " events");
			});
			return eventCount;
		}
		/// <summary>
		/// Recomputes and updates the HashValue of this event in the database.
		/// </summary>
		/// <param name="ev">Event to update the HashValue of.</param>
		/// <returns></returns>
		public void UpdateEventHashId(Event ev)
		{
			if (ev.ComputeHash())
			{
				if (ev.EventId != 0)
				{
					ExecuteNonQuery("UPDATE " + ProjectNameLower + ".Event SET HashValue = @hasharg WHERE EventId = " + ev.EventId, new { hasharg = ev.HashValue });
				}
			}
		}

		/// <summary>
		/// Counts the number of events in the entire project.
		/// </summary>
		/// <returns></returns>
		public long CountEvents()
		{
			return ExecuteScalar<long>("SELECT COUNT(*) FROM " + ProjectNameLower + ".Event");
		}

		/// <summary>
		/// Counts the number of unique events in the entire project (uniqueness determined by matching HashValue).
		/// </summary>
		/// <returns></returns>
		public long CountUniqueEvents()
		{
			return ExecuteScalar<long>("SELECT COUNT(DISTINCT HashValue) FROM " + ProjectNameLower + ".Event");
		}

		/// <summary>
		/// Counts the number of unique events in the entire project (uniqueness determined by matching HashValue).
		/// </summary>
		/// <returns></returns>
		public long CountEventsWithHashValue(string HashValue)
		{
			return ExecuteScalar<long>("SELECT COUNT(EventId) FROM " + ProjectNameLower + ".Event WHERE HashValue = @hasharg", new { hasharg = HashValue });
		}
		#endregion
		#region Search
		/// <summary>
		/// Queries for all events that contain the SQL Full-Text Search query within their Tags, Message, EventType, SubType, Color, or Date fields.
		/// </summary>
		/// <param name="folderId">Folder ID.  Negative ID matches All Folders.</param>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <param name="query">Search query. Not a regular expression.</param>
		/// <returns></returns>
		public List<Event> SqlSearch(int folderId, string eventListCustomTagKey, string query)
		{
			List<string> selections = new List<string>();
			selections.Add("e.*");
			if (!string.IsNullOrWhiteSpace(eventListCustomTagKey))
				selections.Add("t.Value AS CTag");

			PetaPoco.Sql request = PetaPoco.Sql.Builder
				.Select(string.Join(", ", selections))
				.From(ProjectNameLower + ".Event e");

			if (!string.IsNullOrWhiteSpace(eventListCustomTagKey))
				request.LeftJoin(ProjectNameLower + ".Tag t").On("e.EventId = t.EventId AND t.Key = @0", eventListCustomTagKey);

			if (folderId > -1)
				request.Where("e.FolderId = @0", folderId);

			request.Where(@"e.EventId IN
(
		SELECT e.EventId
		FROM %PR.Event e
		WHERE to_tsvector('english', %PR.EventTypeToString(EventType) || ': ' || SubType || ': ' || %PR.ColorToString(Color) || ': ' || %PR.DateToString(Date) || ': ' || Message)
			@@@ websearch_to_tsquery('english', @0)
	UNION
		SELECT EventId
		FROM %PR.Tag
		WHERE to_tsvector('english', Value) @@@ websearch_to_tsquery('english', @0)
)".Replace("%PR", ProjectNameLower), query);

			if (!string.IsNullOrWhiteSpace(eventListCustomTagKey))
				return ExecuteQuery<EventWithCustomTagValue>(request).Cast<Event>().ToList();
			else
				return ExecuteQuery<Event>(request);
		}
		/// <summary>
		/// Gets all events that have a value containing the search query.
		/// </summary>
		/// <param name="folderId">Folder ID.  Negative ID matches All Folders.</param>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <param name="query">Search query. Not a regular expression.</param>
		/// <returns></returns>
		public List<Event> BasicDumbSearch(int folderId, string eventListCustomTagKey, string query)
		{
			string rxQuery = Regex.Escape(query);

			List<string> selections = new List<string>();
			selections.Add("e.*");
			if (!string.IsNullOrWhiteSpace(eventListCustomTagKey))
				selections.Add("t.Value AS CTag");

			PetaPoco.Sql request = PetaPoco.Sql.Builder
				.Select(string.Join(", ", selections))
				.From(ProjectNameLower + ".Event e");

			if (!string.IsNullOrWhiteSpace(eventListCustomTagKey))
				request.LeftJoin(ProjectNameLower + ".Tag t").On("e.EventId = t.EventId AND t.Key = @0", eventListCustomTagKey);

			if (folderId > -1)
				request.Where("e.FolderId = @0", folderId);

			if (!string.IsNullOrEmpty(query))
			{
				StringBuilder sbEventTypeMatcher = new StringBuilder();
				foreach (EventType et in Enum.GetValues(typeof(EventType)))
				{
					if (et.ToString().Contains(query))
					{
						if (sbEventTypeMatcher.Length == 0)
							sbEventTypeMatcher.Append("(");
						else
							sbEventTypeMatcher.Append(" OR ");
						sbEventTypeMatcher.Append("e.EventType = " + (int)et);
					}
				}
				string conditionClause = "(e.Message ~* @0) OR (e.SubType ~* @1)";
				if (sbEventTypeMatcher.Length != 0)
				{
					sbEventTypeMatcher.Append(")");
					conditionClause += " OR " + sbEventTypeMatcher.ToString();
				}
				conditionClause += " OR EXISTS(SELECT * FROM " + ProjectNameLower + ".Tag tc WHERE tc.EventId = e.EventId AND tc.Value ~* @2)";
				request.Where(conditionClause, rxQuery, rxQuery, rxQuery);
			}

			if (!string.IsNullOrWhiteSpace(eventListCustomTagKey))
				return ExecuteQuery<EventWithCustomTagValue>(request).Cast<Event>().ToList();
			else
				return ExecuteQuery<Event>(request);
		}
		/// <summary>
		/// Gets all events that have a value containing the search query.
		/// </summary>
		/// <param name="folderId">Folder ID.  Negative ID matches All Folders.</param>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <param name="conditions">Array of filter conditions to evaluate. The FilterCondition.Enabled field is disregarded.</param>
		/// <param name="matchAll">If true, only one of the conditions needs to match. If false, all conditions need to match.</param>
		/// <returns></returns>
		public List<Event> AdvancedSearch(int folderId, string eventListCustomTagKey, FilterCondition[] conditions, bool matchAll)
		{
			// Check conditions array
			if (conditions == null)
				return new List<Event>();
			conditions = conditions.Where(c => c.Enabled).ToArray();
			if (conditions.Length == 0)
				return new List<Event>();

			// Begin building query
			List<string> selections = new List<string>();
			selections.Add("e.*");
			if (!string.IsNullOrWhiteSpace(eventListCustomTagKey))
				selections.Add("t.Value AS CTag");

			PetaPoco.Sql request = PetaPoco.Sql.Builder
				.Select(string.Join(", ", selections))
				.From(ProjectNameLower + ".Event e");

			if (!string.IsNullOrWhiteSpace(eventListCustomTagKey))
				request.LeftJoin(ProjectNameLower + ".Tag t").On("e.EventId = t.EventId AND t.Key = @0", eventListCustomTagKey);

			bool didAddWhereClause = false;
			if (folderId > -1)
			{
				request.Where("e.FolderId = @0", folderId);
				didAddWhereClause = true;
			}

			// Create SQL that applies the conditions
			HashSet<string> reservedKeys = new HashSet<string>(new string[] {
				"message", "subtype", "eventtype", "color", "date"/*, "folder" << NOT AVAILABLE CURRENTLY because these are not strings in the database. */
			});
			// First, figure out how each condition translates to SQL
			List<Tuple<string, string>> eventConditions = new List<Tuple<string, string>>();
			List<Tuple<string, string, string>> tagConditions = new List<Tuple<string, string, string>>();
			foreach (FilterCondition condition in conditions)
			{
				string op = condition.Invert ? "!~*" : "~*";
				string pattern = condition.Query;
				switch (condition.Operator)
				{
					case FilterConditionOperator.Contains:
						if (!condition.Regex)
							pattern = Regex.Escape(pattern);
						break;
					case FilterConditionOperator.Equals:
						if (condition.Regex)
						{
							if (!pattern.StartsWith("^"))
								pattern = "^" + pattern;
							if (!pattern.EndsWith("$"))
								pattern = pattern + "$";
						}
						else
							pattern = "^" + Regex.Escape(pattern) + "$";
						break;
					case FilterConditionOperator.StartsWith:
						if (condition.Regex)
						{
							if (!pattern.StartsWith("^"))
								pattern = "^" + pattern;
						}
						else
							pattern = "^" + Regex.Escape(pattern);
						break;
					case FilterConditionOperator.EndsWith:
						if (condition.Regex)
						{
							if (!pattern.EndsWith("$"))
								pattern = pattern + "$";
						}
						else
							pattern = Regex.Escape(pattern) + "$";
						break;
					default:
						throw new ApplicationException("Unrecognized FilterConditionOperator: " + condition.Operator + ". Unable to perform advanced search.");
				}
				if (reservedKeys.Contains(condition.TagKey.ToLower()))
				{
					string columnSelector = "e." + condition.TagKey.ToLower();

					if (condition.TagKey.Equals("EventType", StringComparison.OrdinalIgnoreCase))
						columnSelector = ProjectNameLower + ".EventTypeToString(" + columnSelector + ")";
					else if (condition.TagKey.Equals("Color", StringComparison.OrdinalIgnoreCase))
						columnSelector = ProjectNameLower + ".ColorToString(" + columnSelector + ")";
					else if (condition.TagKey.Equals("Date", StringComparison.OrdinalIgnoreCase))
						columnSelector = ProjectNameLower + ".DateToString(" + columnSelector + ")";

					eventConditions.Add(new Tuple<string, string>(columnSelector + " " + op + " @0", pattern));
				}
				else
					tagConditions.Add(new Tuple<string, string, string>("t.Key = @0 AND t.Value " + op + " @1", condition.TagKey, pattern));
			}

			// Now create the SQL WHERE clause.
			string joiner = matchAll ? "AND" : "OR";
			// We may have 0 or more event conditions and 0 or more tag conditions, which complicates how we construct the SQL string.
			if (!didAddWhereClause)
			{
				request.Append("WHERE ("); // Open main condition block
				didAddWhereClause = true;
			}
			else
				request.Append("AND ("); // Open main condition block
			bool firstCondition = true;
			foreach (Tuple<string, string> eventCondition in eventConditions)
			{
				request.Append("  " + (firstCondition ? "" : (joiner + " ")) + eventCondition.Item1, eventCondition.Item2);
				firstCondition = false;
			}

			if (tagConditions.Count > 0)
			{
				// Add a subquery to check for a matching tag.
				// Begin EXISTS check, Open section B.
				request.Append("  " + (firstCondition ? "" : (joiner + " ")) + "EXISTS (\n"
					+ "    SELECT * FROM " + ProjectNameLower + ".Tag t WHERE t.EventId = e.EventId \n"
					+ "      AND (");

				// Set firstCondition to true because we're starting a new condition block.
				firstCondition = true;
				foreach (Tuple<string, string, string> tagCondition in tagConditions)
				{
					request.Append("        " + (firstCondition ? "" : (joiner + " ")) + "(" + tagCondition.Item1 + ")", tagCondition.Item2, tagCondition.Item3);
					firstCondition = false;
				}
				request.Append("      )"); // Close tag condition block
				request.Append("  )"); // Close EXISTS block
			}

			if (didAddWhereClause)
				request.Append(")"); // Close main condition block
			else
				throw new ApplicationException("DB.AdvancedSearch started with " + conditions.Length + " enabled conditions but did not produce any SQL conditions");

			if (!string.IsNullOrWhiteSpace(eventListCustomTagKey))
				return ExecuteQuery<EventWithCustomTagValue>(request).Cast<Event>().ToList();
			else
				return ExecuteQuery<Event>(request);
		}
		#endregion
		#region Folder Management
		/// <summary>
		/// Gets a flat list of all folders.
		/// </summary>
		/// <returns></returns>
		public List<Folder> GetAllFolders()
		{
			List<Folder> folders = QueryAll<Folder>();
			folders.Add(new Folder(ProjectName, 0));
			return folders;
		}
		/// <summary>
		/// Gets the complete folder structure as a tree.
		/// </summary>
		/// <returns></returns>
		public FolderStructure GetFolderStructure()
		{
			return FolderStructure.Build(GetAllFolders());
		}
		/// <summary>
		/// Attempts to add a folder, returning true if successful. Returns false if a folder by that name already exists or if the parent folder does not exist.
		/// </summary>
		/// <param name="folderName">Name of the folder to create.</param>
		/// <param name="parentFolderId">ID of the folder that will contain the new folder.</param>
		/// <param name="errorMessage">[out] string assigned an appropriate error message when the method returns false</param>
		/// <param name="newFolder">[out] the new folder when the method returns true</param>
		/// <returns></returns>
		public bool AddFolder(string folderName, int parentFolderId, out string errorMessage, out Folder newFolder)
		{
			folderName = folderName.Trim();
			if (!Folder.ValidateName(folderName))
			{
				errorMessage = "Invalid folder name.";
				newFolder = null;
				return false;
			}
			Folder nf = null;
			string eMsg = null;
			RunInTransaction(() =>
			{
				FolderStructure root = GetFolderStructure();
				if (root.TryGetNode(parentFolderId, out FolderStructure parent))
				{
					if (parent.GetChild(folderName) == null)
					{
						nf = new Folder(folderName, parentFolderId);
						Insert(nf);
					}
					else
						eMsg = "A folder with this name already exists in the specified location.";
				}
				else
					eMsg = "Parent folder (ID: " + parentFolderId + ") was not found.";
			});
			if (eMsg != null)
				nf = null;
			errorMessage = eMsg;
			newFolder = nf;
			return errorMessage == null;
		}
		/// <summary>
		/// Attempts to move a folder, returning true if successful. Returns false if the folder to move does not exist, if a folder by that name already exists at the destination, or if the destination folder does not exist.
		/// </summary>
		/// <param name="folderId">ID of the folder to move.</param>
		/// <param name="newParentFolderId">ID of the folder to move into.</param>
		/// <param name="errorMessage">[out] string assigned an appropriate error message when the method returns false</param>
		/// <returns></returns>
		public bool MoveFolder(int folderId, int newParentFolderId, out string errorMessage)
		{
			if (folderId == 0)
			{
				errorMessage = "The root folder cannot be moved.";
				return false;
			}
			if (folderId == newParentFolderId)
			{
				errorMessage = "Cannot move folder into itself.";
				return false;
			}
			string eMsg = null;
			RunInTransaction(() =>
			{
				FolderStructure root = GetFolderStructure();
				if (root.TryGetNode(folderId, out FolderStructure current))
				{
					if (newParentFolderId != current.Parent.FolderId)
					{
						if (root.TryGetNode(newParentFolderId, out FolderStructure newParent))
						{
							if (newParent.GetChild(current.Name) == null)
							{
								if (newParent.HasPathToRoot(out HashSet<long> nodesOnRootPath))
								{
									if (!nodesOnRootPath.Contains(folderId))
										ExecuteNonQuery("UPDATE " + ProjectNameLower + ".Folder SET ParentFolderId = " + newParentFolderId + " WHERE FolderId = " + folderId);
									else
										eMsg = "Moving this folder to the specified location would create a circular reference.";
								}
								else
									eMsg = "An unresolved circular reference is preventing this folder from being moved.  This should be impossible and suggests database corruption or an application logic error.";
							}
							else
								eMsg = "A folder with this name already exists in the specified location.";
						}
						else
							eMsg = "Parent folder (ID: " + newParentFolderId + ") was not found.";
					}
					else
						eMsg = "This folder is already in the specified location.";
				}
				else
					eMsg = "Source folder ID: " + folderId + " was not found.";
			});
			errorMessage = eMsg;
			return errorMessage == null;
		}
		/// <summary>
		/// Attempts to rename a folder, returning true if successful. Returns false if the folder to be renamed does not exist or if a folder with the new name already exists at the location.
		/// </summary>
		/// <param name="folderId">ID of the folder to rename.</param>
		/// <param name="newFolderName">New folder name.</param>
		/// <param name="errorMessage">[out] string assigned an appropriate error message when the method returns false</param>
		/// <returns></returns>
		public bool RenameFolder(int folderId, string newFolderName, out string errorMessage)
		{
			newFolderName = newFolderName.Trim();
			if (folderId == 0)
			{
				errorMessage = "The root folder cannot be renamed.";
				return false;
			}
			if (!Folder.ValidateName(newFolderName))
			{
				errorMessage = "Invalid folder name.";
				return false;
			}
			string eMsg = null;
			RunInTransaction(() =>
			{
				FolderStructure root = GetFolderStructure();
				if (root.TryGetNode(folderId, out FolderStructure current))
				{
					if (current.Parent.GetChild(newFolderName) == null)
						ExecuteNonQuery("UPDATE " + ProjectNameLower + ".Folder SET Name = newname WHERE FolderId = " + folderId, new { newname = newFolderName });
					else
						eMsg = "A folder with this name already exists in the location.";
				}
				else
					eMsg = "Folder ID: " + folderId + " was not found.";
			});
			errorMessage = eMsg;
			return errorMessage == null;
		}
		/// <summary>
		/// Attempts to delete a folder, returning true if successful. Returns false if the folder to be deleted does not exist or if the folder contains Events or other folders.
		/// </summary>
		/// <param name="folderId"></param>
		/// <param name="errorMessage"></param>
		/// <returns></returns>
		public bool DeleteFolder(int folderId, out string errorMessage)
		{
			if (folderId == 0)
			{
				errorMessage = "The root folder cannot be deleted.";
				return false;
			}
			string eMsg = null;
			RunInTransaction(() =>
			{
				FolderStructure root = GetFolderStructure();
				if (root.TryGetNode(folderId, out FolderStructure current))
				{
					if (current.Children.Count == 0)
					{
						long eventCount = CountEventsInFolder(folderId);
						if (eventCount == 0)
						{
							Delete<Folder>(folderId);
							List<User> users = Settings.data.GetAllUsers();
							foreach (User u in users)
								u.DeletePushNotificationSubscriptionsByFolder(ProjectName, folderId);
						}
						else
							eMsg = "Folder \"" + current.Name + "\" (ID " + folderId + ") cannot be deleted because it contains " + eventCount + " events.";
					}
					else
						eMsg = "Folder \"" + current.Name + "\" (ID " + folderId + ") cannot be deleted because it contains " + current.Children.Count + " child folders.";
				}
				else
					eMsg = "Folder ID: " + folderId + " was not found.";
			});
			errorMessage = eMsg;
			return errorMessage == null;
		}

		/// <summary>
		/// Gets the folder with the specified ID, or null if the folder is not found.
		/// </summary>
		/// <param name="folderId">Folder ID to look up.</param>
		/// <returns></returns>
		public Folder GetFolder(long folderId)
		{
			List<Folder> folders = Query<Folder>("FolderId", folderId);
			if (folders.Count > 0)
				return folders[0];
			return null;
		}
		/// <summary>
		/// Attempts to return the FolderStructure at the specified path, creating missing folders if necessary.
		/// </summary>
		/// <param name="path">Path to resolve.</param>
		/// <returns></returns>
		public FolderStructure FolderResolvePath(string path)
		{
			FolderStructure fs = null;
			RunInTransaction(() =>
			{
				fs = GetFolderStructure();
				if (!string.IsNullOrWhiteSpace(path))
					fs = fs.ResolvePath(path, this);
			});
			return fs;
		}
		#endregion
		#region Filter Management
		/// <summary>
		/// Gets a list of FilterSummary for all filters in the database.
		/// </summary>
		/// <returns></returns>
		public List<FilterSummary> GetAllFiltersSummary()
		{
			List<Filter> filters = null;
			List<FilterItemCount> conditions = null;
			List<FilterItemCount> actions = null;
			RunInTransaction(() =>
			{
				filters = QueryAll<Filter>();
				conditions = ExecuteQuery<FilterItemCount>("SELECT FilterId, COUNT(FilterConditionId) as Count FROM " + ProjectNameLower + ".FilterCondition WHERE Enabled = true GROUP BY FilterId");
				actions = ExecuteQuery<FilterItemCount>("SELECT FilterId, COUNT(FilterActionId) as Count FROM " + ProjectNameLower + ".FilterAction WHERE Enabled = true GROUP BY FilterId");
			});
			Dictionary<int, uint> filterConditionCounts = conditions.ToDictionary(fic => fic.FilterId, fic => fic.Count);
			Dictionary<int, uint> filterActionCounts = actions.ToDictionary(fic => fic.FilterId, fic => fic.Count);
			return filters
					.Select(f =>
					{
						FilterSummary summary = new FilterSummary() { filter = f };
						if (filterConditionCounts.TryGetValue(f.FilterId, out uint conditionCount))
							summary.NumConditions = conditionCount;
						if (filterActionCounts.TryGetValue(f.FilterId, out uint actionCount))
							summary.NumActions = actionCount;
						return summary;
					})
					.OrderBy(f => f.filter.MyOrder)
					.ThenBy(f => f.filter.FilterId)
					.ToList();
		}
		/// <summary>
		/// Gets a list of FullFilter for all filters in the database, optionally returning only the filters which are enabled.
		/// </summary>
		/// <param name="onlyEnabledFilters">If true, only enabled filters will be returned.</param>
		/// <returns></returns>
		public List<FullFilter> GetFilters(bool onlyEnabledFilters)
		{
			List<Filter> filters = null;
			List<FilterCondition> conditions = null;
			List<FilterAction> actions = null;
			RunInTransaction(() =>
			{
				if (onlyEnabledFilters)
					filters = ExecuteQuery<Filter>("SELECT * FROM " + ProjectNameLower + ".Filter WHERE Enabled = true");
				else
					filters = QueryAll<Filter>();
				conditions = QueryAll<FilterCondition>();
				actions = QueryAll<FilterAction>();
			});

			return filters
					.Select(f =>
					{
						FullFilter full = new FullFilter();
						full.filter = f;
						full.conditions = conditions.Where(c => c.FilterId == f.FilterId).ToArray();
						full.actions = actions.Where(a => a.FilterId == f.FilterId).ToArray();
						return full;
					})
					.OrderBy(f => f.filter.MyOrder)
					.ThenBy(f => f.filter.FilterId)
					.ToList();
		}
		/// <summary>
		/// Gets full details for the filter with the specified FilterId.
		/// </summary>
		/// <param name="filterId">ID of the filter to get.</param>
		/// <returns></returns>
		public FullFilter GetFilter(int filterId)
		{
			List<Filter> filters = null;
			List<FilterCondition> conditions = null;
			List<FilterAction> actions = null;
			RunInTransaction(() =>
			{
				filters = Query<Filter>("FilterId", filterId);
				if (filters.Count == 1)
				{
					conditions = ExecuteQuery<FilterCondition>("SELECT * FROM " + ProjectNameLower + ".FilterCondition WHERE FilterId = " + filterId + " ORDER BY FilterConditionId");
					actions = ExecuteQuery<FilterAction>("SELECT * FROM " + ProjectNameLower + ".FilterAction WHERE FilterId = " + filterId + " ORDER BY FilterActionId");
				}
			});
			if (filters.Count != 1)
				return null;
			FullFilter f = new FullFilter();
			f.filter = filters[0];
			f.conditions = conditions.ToArray();
			f.actions = actions.ToArray();
			return f;
		}
		/// <summary>
		/// Tries to add the specified filter to the database, returning true if successful or false if an error occurred (see errorMessage out param). The filter is added at the end/bottom of the filter list.
		/// </summary>
		/// <param name="newFilter">Filter to add.  The Name field must be set before passing it in here.  The FilterId field will be set upon successful addition to the database.</param>
		/// <param name="conditions">Array of conditions to add to the new filter. May be null or empty.</param>
		/// <param name="errorMessage">Error message will be set if this method returns false.</param>
		/// <returns></returns>
		public bool AddFilter(Filter newFilter, FilterCondition[] conditions, out string errorMessage)
		{
			if (!StringUtil.IsPrintableName(newFilter?.Name))
			{
				errorMessage = "Invalid filter name. Name must be entirely ASCII-printable (ASCII [32-126]) and contain at least one alphanumeric character.";
				return false;
			}
			newFilter.Name = newFilter.Name.Trim();
			string emsg = null;
			RunInTransaction(() =>
			{
				try
				{
					List<Filter> existingFilters = ExecuteQuery<Filter>("SELECT * FROM " + ProjectNameLower + ".Filter WHERE Name = @namearg", new { namearg = newFilter.Name });
					if (existingFilters.Count > 0)
						emsg = "A filter already exists with that name.";
					else
					{
						int highestOrder = 0;
						foreach (FilterSummary sum in GetAllFiltersSummary())
							if (sum.filter.MyOrder > highestOrder)
								highestOrder = sum.filter.MyOrder;
						newFilter.MyOrder = highestOrder + 1;

						Insert(newFilter);

						if (conditions != null && conditions.Length > 0)
						{
							foreach (FilterCondition c in conditions)
							{
								c.FilterId = newFilter.FilterId;
								Insert(c);
							}
						}
					}
				}
				catch
				{
					newFilter = null;
					throw;
				}
			});
			errorMessage = emsg;
			return errorMessage == null;
		}
		/// <summary>
		/// Updates the specified filter with its conditions and actions. Conditions and actions cannot be added or removed via this method. The filter to edit is identified by FilterId. Returns true if successful.
		/// </summary>
		/// <param name="full">A filter with conditions and actions.  Any fields except the ID fields may be updated.</param>
		/// <param name="errorMessage">Error message will be set if this method returns false.</param>
		/// <returns></returns>
		public bool EditFilter(FullFilter full, out string errorMessage)
		{
			if (full.conditions.Any(c => c.FilterId != full.filter.FilterId))
			{
				errorMessage = "Cannot change a Condition's FilterId.";
				return false;
			}
			if (full.actions.Any(a => a.FilterId != full.filter.FilterId))
			{
				errorMessage = "Cannot change an Action's FilterId.";
				return false;
			}
			string emsg = null;
			RunInTransaction(() =>
			{
				List<Filter> existing = Query<Filter>("FilterId", full.filter.FilterId);
				if (existing.Count == 0)
					emsg = "Update Failed.  Unable to find filter with ID " + full.filter.FilterId + ".";
				else
				{
					if (Update(full.filter) == 0)
					{
						emsg = "Failed to update filter with ID " + full.filter.FilterId + " for an unknown reason.";
					}
					else
					{
						UpdateAll(full.conditions);
						UpdateAll(full.actions);
					}
				}
			});
			errorMessage = emsg;
			return errorMessage == null;
		}

		/// <summary>
		/// Deletes the specified filter and all its conditions and actions. Returns true if successful.
		/// </summary>
		/// <param name="filterId">ID of the filter to delete.</param>
		/// <returns></returns>
		public bool DeleteFilter(int filterId)
		{
			int affectedRows = 0;
			RunInTransaction(() =>
			{
				affectedRows += ExecuteNonQuery("DELETE FROM " + ProjectNameLower + ".Filter WHERE FilterId = " + filterId);
				affectedRows += ExecuteNonQuery("DELETE FROM " + ProjectNameLower + ".FilterCondition WHERE FilterId = " + filterId);
				affectedRows += ExecuteNonQuery("DELETE FROM " + ProjectNameLower + ".FilterAction WHERE FilterId = " + filterId);
			});
			if (affectedRows > 0)
				return true;
			return false;
		}
		/// <summary>
		/// Returns true if the filter with the specified ID exists.
		/// </summary>
		/// <param name="filterId">Filter ID to look up.</param>
		/// <returns></returns>
		public bool FilterExists(int filterId)
		{
			return ExecuteScalar<int>("SELECT COUNT(*) FROM " + ProjectNameLower + ".Filter WHERE FilterId = " + filterId) > 0;
		}

		/// <summary>
		/// Sets the MyOrder field as specified for each of the listed filters.
		/// </summary>
		/// <param name="newOrder">An array specifying the order to assign to each FilterId.</param>
		public void ReorderFilters(FilterOrder[] newOrder)
		{
			if (newOrder.Length == 0)
				return; // true;
			int affectedRows = 0;
			RunInTransaction(() =>
			{
				foreach (FilterOrder orderItem in newOrder)
					affectedRows += ExecuteNonQuery("UPDATE " + ProjectNameLower + ".Filter SET MyOrder = " + orderItem.MyOrder + " WHERE FilterId = " + orderItem.FilterId);
			});
			return; // affectedRows == newOrder.Length;
		}
		#endregion
		#region FilterCondition Management
		/// <summary>
		/// Adds the specified FilterCondition and sets its FilterConditionId field. Returns true if successful.
		/// </summary>
		/// <param name="filterCondition">A FilterCondition to add.</param>
		/// <param name="errorMessage">Error message will be set if this method returns false.</param>
		/// <returns></returns>
		public bool AddFilterCondition(FilterCondition filterCondition, out string errorMessage)
		{
			string emsg = null;
			RunInTransaction(() =>
			{
				if (!FilterExists(filterCondition.FilterId))
					emsg = "Could not find filter with ID " + filterCondition.FilterId;
				else
					Insert(filterCondition);
			});
			errorMessage = emsg;
			return errorMessage == null;
		}
		/// <summary>
		/// Updates the specified FilterCondition. Returns true if successful.
		/// </summary>
		/// <param name="filterCondition">A FilterCondition with updated fields.</param>
		/// <param name="errorMessage">Error message will be set if this method returns false.</param>
		/// <returns></returns>
		public bool EditFilterCondition(FilterCondition filterCondition, out string errorMessage)
		{
			string emsg = null;
			RunInTransaction(() =>
			{
				if (!FilterExists(filterCondition.FilterId))
					emsg = "Could not find filter with ID " + filterCondition.FilterId;
				else
					Update(filterCondition);
			});
			errorMessage = emsg;
			return errorMessage == null;
		}
		/// <summary>
		/// Deletes the specified FilterCondition. Returns true if successful.
		/// </summary>
		/// <param name="filterConditionId">FilterConditionId of the FilterCondition to delete.</param>
		/// <returns></returns>
		public bool DeleteFilterCondition(int filterConditionId)
		{
			int rowsUpdated = Delete<FilterCondition>(filterConditionId);
			return rowsUpdated == 1;
		}
		#endregion
		#region FilterAction Management
		/// <summary>
		/// Adds the specified FilterAction and sets its FilterActionId field. Returns true if successful.
		/// </summary>
		/// <param name="filterAction">A FilterAction to add.</param>
		/// <param name="errorMessage">Error message will be set if this method returns false.</param>
		/// <returns></returns>
		public bool AddFilterAction(FilterAction filterAction, out string errorMessage)
		{
			string emsg = null;
			RunInTransaction(() =>
			{
				if (!FilterExists(filterAction.FilterId))
					emsg = "Could not find filter with ID " + filterAction.FilterId;
				else
					Insert(filterAction);
			});
			errorMessage = emsg;
			return errorMessage == null;
		}
		/// <summary>
		/// Updates the specified FilterAction. Returns true if successful.
		/// </summary>
		/// <param name="filterAction">A FilterAction with updated fields.</param>
		/// <param name="errorMessage">Error message will be set if this method returns false.</param>
		/// <returns></returns>
		public bool EditFilterAction(FilterAction filterAction, out string errorMessage)
		{
			string emsg = null;
			RunInTransaction(() =>
			{
				if (!FilterExists(filterAction.FilterId))
					emsg = "Could not find filter with ID " + filterAction.FilterId;
				else
					Update(filterAction);
			});
			errorMessage = emsg;
			return errorMessage == null;
		}
		/// <summary>
		/// Deletes the specified FilterAction. Returns true if successful.
		/// </summary>
		/// <param name="filterActionId">FilterActionId of the FilterAction to delete.</param>
		/// <returns></returns>
		public bool DeleteFilterAction(int filterActionId)
		{
			int rowsUpdated = Delete<FilterAction>(filterActionId);
			return rowsUpdated == 1;
		}
		#endregion
		#region ReadState Management
		/// <summary>
		/// Remembers that a user has read a specific event.
		/// </summary>
		/// <param name="userId">ID of the User reading the event.</param>
		/// <param name="eventId">ID of the Event which was read.</param>
		public void AddReadState(int userId, long eventId)
		{
			RunInTransaction(() =>
			{
				if (!ExistsReadState(userId, eventId))
					Insert(new ReadState() { UserId = userId, EventId = eventId });
			});
		}
		/// <summary>
		/// Remembers that a user has read specific events.
		/// </summary>
		/// <param name="userId">ID of the User reading the event.</param>
		/// <param name="eventIds">IDs of the Events which were read.</param>
		public void AddReadState(int userId, IEnumerable<long> eventIds)
		{
			if (eventIds.Count() > 0)
				RunInTransaction(() =>
				{
					RemoveReadState(userId, eventIds);
					string query = "INSERT INTO " + ProjectNameLower + ".ReadState (UserId, EventId) VALUES " + string.Join(", ", eventIds.Select(eid => "(" + userId + "," + eid + ")"));
					ExecuteNonQuery(query);
				});
		}
		/// <summary>
		/// Forgets that a user has read a specific event, returning true if successful or false if a matching ReadState was not found.
		/// </summary>
		/// <param name="userId">ID of the User which read the event.</param>
		/// <param name="eventId">ID of the Event which was read.</param>
		/// <returns></returns>
		public bool RemoveReadState(int userId, long eventId)
		{
			int affectedRows = ExecuteNonQuery("DELETE FROM " + ProjectNameLower + ".ReadState WHERE UserId = " + userId + " AND EventId =" + eventId);
			return affectedRows == 1;
		}
		/// <summary>
		/// Forgets that a user has read specific events.
		/// </summary>
		/// <param name="userId">ID of the User which read the event.</param>
		/// <param name="eventIds">IDs of the Events which were unread.</param>
		/// <returns></returns>
		public void RemoveReadState(int userId, IEnumerable<long> eventIds)
		{
			if (eventIds.Count() > 0)
				ExecuteNonQuery("DELETE FROM " + ProjectNameLower + ".ReadState WHERE UserId = " + userId + " AND EventId IN (" + string.Join(",", eventIds) + ")");
		}
		/// <summary>
		/// Removes all read states for the specified User, returning the number of read states that were removed.
		/// </summary>
		/// <param name="userId">User ID to forget read history for.</param>
		/// <returns></returns>
		public int RemoveAllReadStates(int userId)
		{
			return ExecuteNonQuery("DELETE FROM " + ProjectNameLower + ".ReadState WHERE UserId = " + userId);
		}
		/// <summary>
		/// Returns true if the user has read the event.
		/// </summary>
		/// <param name="userId">ID of the User which read the event.</param>
		/// <param name="eventId">ID of the Event which was read.</param>
		/// <returns></returns>
		public bool ExistsReadState(int userId, long eventId)
		{
			return ExecuteScalar<int>("SELECT COUNT(*) FROM " + ProjectNameLower + ".ReadState WHERE UserId = " + userId + " AND EventId = " + eventId) > 0;
		}
		/// <summary>
		/// Returns an array of EventId for Events which the user has read.
		/// </summary>
		/// <param name="userId">ID of the User to query read state for.</param>
		/// <returns></returns>
		public long[] GetAllReadEventIds(int userId)
		{
			return ExecuteQuery<ReadState>("SELECT * FROM " + ProjectNameLower + ".ReadState WHERE UserId = " + userId).Select(r => r.EventId).ToArray();
		}
		/// <summary>
		/// Returns a List of all ReadState currently in this database.
		/// </summary>
		/// <returns></returns>
		public List<ReadState> GetAllReadStates()
		{
			return QueryAll<ReadState>();
		}
		#endregion
		public static string TestSqlBuilder()
		{
			string arg1 = "one";
			string arg2 = "two";
			string arg3 = "three";
			PetaPoco.Sql request = PetaPoco.Sql.Builder
				.Select("*")
				.From("Event e")
				.Where("e.Message = @0", arg1)
				.Append("AND (e.SubType = @0 || e.SubType = @1)", arg2, arg3);
			return request.SQL + "\r\n\r\n" + string.Join(", ", request.Arguments);
		}
	}
}

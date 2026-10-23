using ErrorTrackerServer.Database.Project.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ErrorTrackerServer.Filtering
{
	/// <summary>
	/// Maintains lists of actions that have been deferred until later so the actions can be performed in batches for efficiency. Not thread safe.
	/// </summary>
	public class DeferredActionCollection : IFilterActionSink
	{
		/// <summary>
		/// Counts what <see cref="ExecuteDeferredActions(DB, bool)"/> has done since this collection was created.  Accumulates across executions.
		/// </summary>
		public FilterRunEffects Effects { get; private set; } = new FilterRunEffects();

		ActionAggregator<int> folderMap = new ActionAggregator<int>();
		/// <summary>
		/// Maps EventId to the folder the event was in before any MoveTo action was scheduled for it.
		/// </summary>
		private Dictionary<long, int> originalFolderIds = new Dictionary<long, int>();
		/// <summary>
		/// Maps FolderId to the folder's absolute path, for effect reporting.
		/// </summary>
		private Dictionary<int, string> folderPaths = new Dictionary<int, string>();
		/// <summary>
		/// Schedules a MoveTo action.  Any previously-scheduled MoveEventTo actions for this Event will be unscheduled.
		/// </summary>
		/// <param name="ev">Event to move.</param>
		/// <param name="folderId">ID of the folder to move the event to.</param>
		public void MoveEventTo(Event ev, int folderId)
		{
			if (!originalFolderIds.ContainsKey(ev.EventId))
				originalFolderIds[ev.EventId] = ev.FolderId;
			folderMap.Set(ev.EventId, folderId);
		}
		/// <summary>
		/// Schedules a MoveTo action.  Any previously-scheduled MoveEventTo actions for this Event will be unscheduled.
		/// </summary>
		/// <param name="ev">Event to move.</param>
		/// <param name="folderId">ID of the folder to move the event to.</param>
		/// <param name="folderPath">Absolute path of the folder, for effect reporting.</param>
		public void MoveEventTo(Event ev, int folderId, string folderPath)
		{
			if (folderId < 0)
				throw new ArgumentException("DeferredActionCollection cannot move an event to a folder that does not exist.", "folderId");
			if (folderPath != null)
				folderPaths[folderId] = folderPath;
			MoveEventTo(ev, folderId);
		}
		/// <summary>
		/// Schedules a MoveTo action.  Any previously-scheduled MoveEventTo actions for this Event will be unscheduled.
		/// </summary>
		/// <param name="eventId">ID of the event to move.</param>
		/// <param name="folderId">ID of the folder to move the event to.</param>
		public void MoveEventTo(long eventId, int folderId)
		{
			folderMap.Set(eventId, folderId);
		}

		ActionAggregator<uint> colorMap = new ActionAggregator<uint>();
		/// <summary>
		/// Maps EventId to the event's color before any SetColor action was scheduled for it.
		/// </summary>
		private Dictionary<long, uint> originalColors = new Dictionary<long, uint>();
		/// <summary>
		/// Schedules a SetColor action.
		/// </summary>
		/// <param name="ev"></param>
		/// <param name="color"></param>
		public void SetEventColor(Event ev, uint color)
		{
			if (!originalColors.ContainsKey(ev.EventId))
				originalColors[ev.EventId] = ev.Color;
			colorMap.Set(ev.EventId, color);
		}

		private HashSet<long> eventIdsToDelete = new HashSet<long>();
		/// <summary>
		/// Schedules a Delete action.
		/// </summary>
		/// <param name="ev"></param>
		public void DeleteEvent(Event ev)
		{
			eventIdsToDelete.Add(ev.EventId);
		}
		ActionAggregator<bool> readStateMap = new ActionAggregator<bool>();
		/// <summary>
		/// Schedules a MarkRead or MarkUnread action.
		/// </summary>
		/// <param name="ev"></param>
		/// <param name="read">If true, the event will be marked as read.  If false, the event will be marked as unread.</param>
		public void SetReadState(Event ev, bool read)
		{
			readStateMap.Set(ev.EventId, read);
		}
		private List<FilterApplied> rememberFilterAppliedList = new List<FilterApplied>();
		/// <summary>
		/// Schedules the addition of a row to the FilterApplied table, remembering that a filter was matched to an event.
		/// </summary>
		/// <param name="ev">Event object.</param>
		/// <param name="filterId">Filter ID that was matched to the event.</param>
		/// <param name="date">Date that the filter was matched to the event (milliseconds since unix epoch).</param>
		public void RememberFilterApplied(Event ev, int filterId, long date)
		{
			rememberFilterAppliedList.Add(new FilterApplied() { EventId = ev.EventId, FilterId = filterId, Date = date });
		}
		/// <summary>
		/// Executes all scheduled actions and resets the state of this collection so it can be reused in a new filtering operation.  Returns true if all scheduled actions executed successfully.
		/// </summary>
		/// <param name="db">Project's database instance.</param>
		public bool ExecuteDeferredActions(DB db)
		{
			return ExecuteDeferredActions(db, false);
		}
		/// <summary>
		/// Executes all scheduled actions and resets the state of this collection so it can be reused in a new filtering operation.  Returns true if all scheduled actions executed successfully.
		/// </summary>
		/// <param name="db">Project's database instance.</param>
		/// <param name="isEventSubmission">If true, additional logging may be performed.</param>
		public bool ExecuteDeferredActions(DB db, bool isEventSubmission)
		{
			try
			{
				bool success = true;

				// MoveTo
				foreach (KeyValuePair<int, List<long>> kvp in folderMap.ReverseMap())
				{
					long[] eventIds = kvp.Value.Where(id => !eventIdsToDelete.Contains(id)).ToArray();
					if (eventIds.Length > 0)
					{
						if (isEventSubmission && Settings.data.verboseSubmitLogging)
							Util.SubmitLog(db.ProjectName, "Event " + string.Join(",", eventIds) + " > MoveTo " + kvp.Key);
						if (!db.MoveEvents(eventIds, kvp.Key))
						{
							success = false;
							if (isEventSubmission && Settings.data.verboseSubmitLogging)
								Util.SubmitLog(db.ProjectName, "Action indicated failure");
						}
						else
						{
							long movedCount = eventIds.Count(id => !originalFolderIds.TryGetValue(id, out int originalFolderId) || originalFolderId != kvp.Key);
							if (!folderPaths.TryGetValue(kvp.Key, out string path))
								path = "Folder ID " + kvp.Key;
							Effects.AddMoved(path, movedCount);
						}
					}
				}

				// SetColor
				foreach (KeyValuePair<uint, List<long>> kvp in colorMap.ReverseMap())
				{
					long[] eventIds = kvp.Value.Where(id => !eventIdsToDelete.Contains(id)).ToArray();
					if (eventIds.Length > 0)
					{
						if (isEventSubmission && Settings.data.verboseSubmitLogging)
							Util.SubmitLog(db.ProjectName, "Event " + string.Join(",", eventIds) + " > SetColor " + kvp.Key);
						if (!db.SetEventsColor(eventIds, kvp.Key))
						{
							success = false;
							if (isEventSubmission && Settings.data.verboseSubmitLogging)
								Util.SubmitLog(db.ProjectName, "Action indicated failure");
						}
						else
							Effects.AddColored(kvp.Key, eventIds.Count(id => !originalColors.TryGetValue(id, out uint originalColor) || originalColor != kvp.Key));
					}
				}

				// Delete
				long[] idsToDelete = eventIdsToDelete.ToArray();
				if (idsToDelete.Length > 0)
				{
					if (isEventSubmission && Settings.data.verboseSubmitLogging)
						Util.SubmitLog(db.ProjectName, "Event " + string.Join(",", idsToDelete) + " > Delete");
					if (!db.DeleteEvents(idsToDelete))
					{
						success = false;
						if (isEventSubmission && Settings.data.verboseSubmitLogging)
							Util.SubmitLog(db.ProjectName, "Action indicated failure");
					}
					else
						Effects.Deleted += idsToDelete.Length;
				}

				// SetReadState
				foreach (KeyValuePair<bool, List<long>> kvp in readStateMap.ReverseMap())
				{
					long[] eventIds = kvp.Value.Where(id => !eventIdsToDelete.Contains(id)).ToArray();
					if (eventIds.Length > 0)
					{
						if (isEventSubmission && Settings.data.verboseSubmitLogging)
							Util.SubmitLog(db.ProjectName, "Event " + string.Join(",", eventIds) + " > SetReadState(" + kvp.Key + ")");
						foreach (User user in Settings.data.GetAllUsers())
						{
							if (kvp.Key)
								db.AddReadState(user.UserId, eventIds);
							else
								db.RemoveReadState(user.UserId, eventIds);
						}
						if (kvp.Key)
							Effects.MarkedRead += eventIds.Length;
						else
							Effects.MarkedUnread += eventIds.Length;
					}
				}

				// RememberFilterApplied
				IEnumerable<FilterApplied> newFilterAppliedRows = rememberFilterAppliedList.Where(fa => !eventIdsToDelete.Contains(fa.EventId));
				db.AddOrUpdateFilterAppliedRows(newFilterAppliedRows);

				return success;
			}
			finally
			{
				folderMap.Clear();
				originalFolderIds.Clear();
				colorMap.Clear();
				originalColors.Clear();
				eventIdsToDelete.Clear();
				readStateMap.Clear();
				rememberFilterAppliedList.Clear();
			}
		}
	}
}

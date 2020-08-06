using ErrorTrackerServer.Database.Project.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ErrorTrackerServer.Filtering
{
	/// <summary>
	/// Maintains lists of actions that have been deferred until later so the actions can be performed in batches for efficiency. Not thread safe.
	/// </summary>
	public class DeferredActionCollection
	{
		ActionAggregator<int> folderMap = new ActionAggregator<int>();
		/// <summary>
		/// Schedules a MoveTo action.  Any previously-scheduled MoveEventTo actions for this Event will be unscheduled.
		/// </summary>
		/// <param name="ev">Event to move.</param>
		/// <param name="folderId">ID of the folder to move the event to.</param>
		public void MoveEventTo(Event ev, int folderId)
		{
			folderMap.Set(ev.EventId, folderId);
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
		/// Schedules a SetColor action.
		/// </summary>
		/// <param name="ev"></param>
		/// <param name="color"></param>
		public void SetEventColor(Event ev, uint color)
		{
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
							Util.SubmitLog("Event " + string.Join(",", eventIds) + " > MoveTo " + kvp.Key);
						if (!db.MoveEvents(eventIds, kvp.Key))
						{
							success = false;
							if (isEventSubmission && Settings.data.verboseSubmitLogging)
								Util.SubmitLog("Action indicated failure");
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
							Util.SubmitLog("Event " + string.Join(",", eventIds) + " > SetColor " + kvp.Key);
						if (!db.SetEventsColor(eventIds, kvp.Key))
						{
							success = false;
							if (isEventSubmission && Settings.data.verboseSubmitLogging)
								Util.SubmitLog("Action indicated failure");
						}
					}
				}

				// Delete
				long[] idsToDelete = eventIdsToDelete.ToArray();
				if (idsToDelete.Length > 0)
				{
					if (isEventSubmission && Settings.data.verboseSubmitLogging)
						Util.SubmitLog("Event " + string.Join(",", idsToDelete) + " > Delete");
					if (!db.DeleteEvents(idsToDelete))
					{
						success = false;
						if (isEventSubmission && Settings.data.verboseSubmitLogging)
							Util.SubmitLog("Action indicated failure");
					}
				}

				// SetReadState
				foreach (KeyValuePair<bool, List<long>> kvp in readStateMap.ReverseMap())
				{
					long[] eventIds = kvp.Value.Where(id => !eventIdsToDelete.Contains(id)).ToArray();
					if (eventIds.Length > 0)
					{
						if (isEventSubmission && Settings.data.verboseSubmitLogging)
							Util.SubmitLog("Event " + string.Join(",", eventIds) + " > SetReadState(" + kvp.Key + ")");
						foreach (User user in Settings.data.GetAllUsers())
						{
							if (kvp.Key)
								db.AddReadState(user.UserId, eventIds);
							else
								db.RemoveReadState(user.UserId, eventIds);
						}
					}
				}

				return success;
			}
			finally
			{
				folderMap.Clear();
				colorMap.Clear();
				eventIdsToDelete.Clear();
			}
		}
	}
}
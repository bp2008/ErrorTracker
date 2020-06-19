using ErrorTrackerServer.Database.Model;
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
		/// <summary>
		/// Executes all scheduled actions and resets the state of this collection so it can be reused in a new filtering operation.  Returns true if all scheduled actions executed successfully.
		/// </summary>
		public bool ExecuteDeferredActions(DB db)
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
						if (!db.MoveEvents(eventIds, kvp.Key))
							success = false;
					}
				}

				// SetColor
				foreach (KeyValuePair<uint, List<long>> kvp in colorMap.ReverseMap())
				{
					long[] eventIds = kvp.Value.Where(id => !eventIdsToDelete.Contains(id)).ToArray();
					if (eventIds.Length > 0)
					{
						if (!db.SetEventsColor(eventIds, kvp.Key))
							success = false;
					}
				}

				// Delete
				long[] idsToDelete = eventIdsToDelete.ToArray();
				if (!db.DeleteEvents(idsToDelete))
					success = false;

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
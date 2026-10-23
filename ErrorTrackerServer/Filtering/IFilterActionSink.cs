using ErrorTrackerServer.Database.Project.Model;
using System.Collections.Generic;
using System.Linq;

namespace ErrorTrackerServer.Filtering
{
	/// <summary>
	/// Receives the actions which <see cref="FilterEngine"/> decides to take against events.  <see cref="DeferredActionCollection"/> schedules them for execution; a dry run records them instead.
	/// </summary>
	public interface IFilterActionSink
	{
		/// <summary>
		/// Schedules a MoveTo action.  Called before the engine updates <c>ev.FolderId</c>, so <c>ev.FolderId</c> is still the event's current folder.
		/// </summary>
		/// <param name="ev">Event to move.</param>
		/// <param name="folderId">ID of the destination folder, or -1 during a dry run if the folder does not exist yet (a real run would create it).</param>
		/// <param name="folderPath">Absolute path of the destination folder.</param>
		void MoveEventTo(Event ev, int folderId, string folderPath);
		/// <summary>
		/// Schedules a Delete action.
		/// </summary>
		/// <param name="ev">Event to delete.</param>
		void DeleteEvent(Event ev);
		/// <summary>
		/// Schedules a SetColor action.  Called before the engine updates <c>ev.Color</c>.
		/// </summary>
		/// <param name="ev">Event to color.</param>
		/// <param name="color">RGB color.</param>
		void SetEventColor(Event ev, uint color);
		/// <summary>
		/// Schedules a MarkRead or MarkUnread action (for all users).
		/// </summary>
		/// <param name="ev">Event.</param>
		/// <param name="read">True for MarkRead, false for MarkUnread.</param>
		void SetReadState(Event ev, bool read);
		/// <summary>
		/// Schedules the addition of a row to the FilterApplied table.
		/// </summary>
		/// <param name="ev">Event which was matched.</param>
		/// <param name="filterId">ID of the filter which matched.</param>
		/// <param name="date">Time of the match in milliseconds since the unix epoch.</param>
		void RememberFilterApplied(Event ev, int filterId, long date);
	}

	/// <summary>
	/// Counts what a filter run did (or, for a dry run, would do).
	/// </summary>
	public class FilterRunEffects
	{
		/// <summary>
		/// Number of events moved, keyed by absolute destination folder path.  Events which were already in the destination folder are not counted.
		/// </summary>
		public Dictionary<string, long> Moved = new Dictionary<string, long>();
		/// <summary>
		/// Number of events deleted.
		/// </summary>
		public long Deleted;
		/// <summary>
		/// Number of events whose color was changed.  Events which already had the color are not counted.
		/// </summary>
		public long Colored;
		/// <summary>
		/// Number of colored events, keyed by "#RRGGBB" color.
		/// </summary>
		public Dictionary<string, long> ColoredByColor = new Dictionary<string, long>();
		/// <summary>
		/// Number of events marked read (for all users).
		/// </summary>
		public long MarkedRead;
		/// <summary>
		/// Number of events marked unread (for all users).
		/// </summary>
		public long MarkedUnread;
		/// <summary>
		/// Absolute paths of folders which were created by MoveTo actions.
		/// </summary>
		public List<string> FoldersCreated = new List<string>();

		internal void AddMoved(string path, long count)
		{
			if (count <= 0)
				return;
			Moved.TryGetValue(path, out long existing);
			Moved[path] = existing + count;
		}
		internal void AddColored(uint color, long count)
		{
			if (count <= 0)
				return;
			Colored += count;
			string key = "#" + (color & 0xFFFFFF).ToString("X6");
			ColoredByColor.TryGetValue(key, out long existing);
			ColoredByColor[key] = existing + count;
		}
	}
}

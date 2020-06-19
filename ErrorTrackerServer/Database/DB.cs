using BPUtil;
using ErrorTrackerServer.Controllers;
using ErrorTrackerServer.Database.Model;
using SQLite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	/// <summary>
	/// Use within a "using" block to guarantee correct disposal.  Provides SQLite database access.  Not thread safe.  
	/// </summary>
	public class DB : IDisposable
	{
		#region Constructor / Fields
		/// <summary>
		/// Database version number useful for performing migrations. This number should only be incremented when migrations are in place to support upgrading all previously existing versions to this version.
		/// </summary>
		public const int dbVersion = 3;
		/// <summary>
		/// Project name.
		/// </summary>
		public readonly string ProjectName;
		/// <summary>
		/// Lower-case project name.
		/// </summary>
		public readonly string ProjectNameLower;
		/// <summary>
		/// File name of the database.
		/// </summary>
		public readonly string DbFilename;
		/// <summary>
		/// Lazy-loaded database connection.
		/// </summary>
		private Lazy<SQLiteConnection> conn;
		/// <summary>
		/// Collection for keeping track of which database files have been initialized during this run of the app.  This could be run every time, but it is only necessary to do it once per app instance.
		/// </summary>
		private static ConcurrentDictionary<string, bool> initializedDatabases = new ConcurrentDictionary<string, bool>();
		private static object createMigrateLock = new object();
		/// <summary>
		/// Use within a "using" block to guarantee correct disposal.  Provides SQLite database access.  Not thread safe.  The DB connection will be lazy-loaded upon the first DB request.
		/// </summary>
		/// <param name="projectName">Project name, case insensitive. Please validate the project name before passing it in, as this class will create the database if it doesn't exist.</param>
		public DB(string projectName)
		{
			ProjectName = projectName;
			ProjectNameLower = projectName.ToLower();
			DbFilename = "ErrorTrackerDB-" + ProjectNameLower + ".s3db";
			conn = new Lazy<SQLiteConnection>(CreateDbConnection, false);
		}
		private SQLiteConnection CreateDbConnection()
		{
			SQLiteConnection c = new SQLiteConnection(Globals.WritableDirectoryBase + "Projects/" + DbFilename, true);
			c.BusyTimeout = TimeSpan.FromSeconds(10);
			CreateOrMigrate(c);
			return c;
		}
		private void CreateOrMigrate(SQLiteConnection c)
		{
			if (!initializedDatabases.TryGetValue(ProjectNameLower, out bool initialized) || !initialized)
			{
				initializedDatabases[ProjectNameLower] = true;
				lock (createMigrateLock) // It is a minor (insignificant) inefficiency having a single lock for all database file initialization.
				{
					if (!initializedDatabases.TryGetValue(ProjectNameLower, out initialized) || !initialized)
						return;

					c.CreateTable<Event>();
					c.CreateTable<Filter>();
					c.CreateTable<FilterAction>();
					c.CreateTable<FilterCondition>();
					c.CreateTable<Tag>();
					c.CreateTable<Folder>();
					c.CreateTable<DbVersion>();

					PerformDbMigrations(c);
				}
			}
		}
		/// <summary>
		/// Performs Db Migrations. As this occurs during lazy connection loading, predefined API methods are unavailable here.
		/// </summary>
		/// <param name="c">The db connection.</param>
		private void PerformDbMigrations(SQLiteConnection c)
		{
			// Get current version
			DbVersion version = c.Query<DbVersion>("SELECT * From DbVersion").FirstOrDefault();
			if (version == null)
			{
				// This is a new DB. It will start at the latest version!
				version = new DbVersion() { CurrentVersion = dbVersion };
				c.Insert(version);
			}

			// Version 1 -> 2 added the HashValue column to Event
			// Version 2 -> 3 began enforcing that all events have a hash value when they are added to the database.
			// Both migrations therefore simply require existing hash values to all be recomputed.
			if (version.CurrentVersion == 1 || version.CurrentVersion == 2)
			{
				c.RunInTransaction(() =>
				{
					version = c.Query<DbVersion>("SELECT * From DbVersion").FirstOrDefault();
					if (version.CurrentVersion == 1 || version.CurrentVersion == 2)
					{
						MigrateComputeHashValuesForAllEvents(c);
						version.CurrentVersion = 3;
						c.Execute("UPDATE DbVersion SET CurrentVersion = ?", version.CurrentVersion);
					}
				});
			}
		}
		private void MigrateComputeHashValuesForAllEvents(SQLiteConnection c)
		{
			foreach (Event ev in c.DeferredQuery<Event>("SELECT * FROM Event"))
				if (ev.ComputeHash())
					c.Execute("UPDATE Event SET HashValue = ? WHERE EventId = ?", ev.HashValue, ev.EventId);
		}
		/// <summary>
		/// True if the database is currently in a transaction.
		/// </summary>
		public bool IsInTransaction
		{
			get
			{
				return conn.IsValueCreated ? conn.Value.IsInTransaction : false;
			}
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
			conn.Value.RunInTransaction(() =>
			{
				conn.Value.Insert(e);
				IEnumerable<Tag> tags = e.GetAllTags();
				conn.Value.InsertAll(tags);
			});
		}
		///// <summary>
		///// Moves the specified event to the specified folder, returning true if successful.
		///// </summary>
		///// <param name="eventId">Event ID to move.</param>
		///// <param name="newFolderId">Folder ID to move the event into.</param>
		///// <returns></returns>
		//public bool MoveEvent(long eventId, long newFolderId)
		//{
		//	if (GetFolder(newFolderId) == null)
		//		return false;
		//	if (conn.Value.ExecuteScalar<int>("UPDATE Event SET FolderId = ? WHERE EventId = ?", newFolderId, eventId) == 1)
		//		return true;
		//	return false;
		//}
		/// <summary>
		/// Moves the specified events to the specified folder, returning true if all of the events were moved. (false if the number of affected rows != eventIds.Length)
		/// </summary>
		/// <param name="eventIds">Event IDs to move.</param>
		/// <param name="newFolderId">Folder ID to move the events into.</param>
		/// <returns></returns>
		public bool MoveEvents(long[] eventIds, long newFolderId)
		{
			if (newFolderId != 0 && GetFolder(newFolderId) == null)
				return false;
			int affectedRows = conn.Value.Execute("UPDATE Event SET FolderId = ? WHERE EventId IN (" + string.Join(",", eventIds) + ")", newFolderId);
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
		//	if (conn.Value.Delete<Event>(eventId) == 1)
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
			conn.Value.RunInTransaction(() =>
			{
				affectedRows = conn.Value.Execute("DELETE FROM Event WHERE EventId IN (" + string.Join(",", eventIds) + ")");
				conn.Value.Execute("DELETE FROM Tag WHERE EventId IN (" + string.Join(",", eventIds) + ")");
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
			int affectedRows = conn.Value.Execute("UPDATE Event SET Color = ? WHERE EventId IN (" + string.Join(",", eventIds) + ")", color);
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
				conn.Value.RunInTransaction(() =>
				{
					conn.Value.InsertAll(events, false);
					List<Tag> allTags = new List<Tag>();
					foreach (Event e in events)
					{
						IEnumerable<Tag> tags = e.GetAllTags();
						allTags.AddRange(tags);
					}
					if (allTags.Count > 0)
						conn.Value.InsertAll(allTags, false);
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
			conn.Value.RunInTransaction(() =>
			{
				events = conn.Value.Query<Event>("SELECT * FROM Event WHERE EventId = ?", eventId);
				if (events.Count > 0)
					tags = conn.Value.Query<Tag>("SELECT Tag.* FROM Tag WHERE EventId = ?", eventId);
			});
			if (events.Count > 0)
			{
				AddTagsToEvents(events, tags);
				return events[0];
			}
			return null;
		}
		/// <summary>
		/// Returns a collection that iterates through all the Events without needing to load them all into memory first. If you need to use the event's tags, you will need to call 
		/// </summary>
		/// <returns></returns>
		public IEnumerable<Event> GetAllEventsNoTagsDeferred()
		{
			return conn.Value.DeferredQuery<Event>("SELECT * FROM Event");
		}
		/// <summary>
		/// Loads the event's tags from the database only if the event currently has no tags defined. Meant to be used with <see cref="GetAllEventsNoTagsDeferred"/>.
		/// </summary>
		/// <param name="ev">The event to load tags into.</param>
		/// <returns></returns>
		public void GetEventTags(Event ev)
		{
			if (ev.GetTagCount() > 0)
				return;
			List<Tag> tags = conn.Value.Query<Tag>("SELECT * FROM Tag WHERE EventId = ?", ev.EventId);
			foreach (Tag t in tags)
				ev.SetTag(t.Key, t.Value);
		}
		/// <summary>
		/// Gets all events within the specified date range.
		/// </summary>
		/// <param name="oldestEpoch">Start date in milliseconds since the unix epoch.</param>
		/// <param name="newestEpoch">End date in milliseconds since the unix epoch.</param>
		/// <returns></returns>
		public List<Event> GetEventsByDate(long oldestEpoch, long newestEpoch)
		{
			List<Event> events = null;
			List<Tag> tags = null;
			conn.Value.RunInTransaction(() =>
			{
				events = conn.Value.Query<Event>("SELECT * FROM Event WHERE Date >= ? AND Date <= ?", oldestEpoch, newestEpoch);
				if (events.Count > 0)
					tags = conn.Value.Query<Tag>("SELECT Tag.* FROM Tag INNER JOIN Event ON Tag.EventId = Event.EventId WHERE Event.Date >= ? AND Event.Date <= ?", oldestEpoch, newestEpoch);
			});
			if (events.Count > 0)
				AddTagsToEvents(events, tags);
			return events;
		}
		/// <summary>
		/// Gets all events within the specified date range. Does not populate the Tags field.
		/// </summary>
		/// <param name="oldestEpoch">Start date in milliseconds since the unix epoch.</param>
		/// <param name="newestEpoch">End date in milliseconds since the unix epoch.</param>
		/// <returns></returns>
		public List<Event> GetEventsWithoutTagsByDate(long oldestEpoch, long newestEpoch)
		{
			List<Event> events = conn.Value.Query<Event>("SELECT * FROM Event WHERE Date >= ? AND Date <= ?", oldestEpoch, newestEpoch);
			return events;
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
			conn.Value.RunInTransaction(() =>
			{
				events = conn.Value.Query<Event>("SELECT * FROM Event WHERE Event.FolderId = ?", folderId);
				if (events.Count > 0)
					tags = conn.Value.Query<Tag>("SELECT Tag.* FROM Tag INNER JOIN Event ON Tag.EventId = Event.EventId WHERE Event.FolderId = ?", folderId);
			});
			if (events.Count > 0)
				AddTagsToEvents(events, tags);
			return events;
		}

		/// <summary>
		/// Gets all events from the specified folder. Does not populate the Tags field.
		/// </summary>
		/// <param name="folderId">Folder ID.</param>
		/// <returns></returns>
		public List<Event> GetEventsWithoutTagsInFolder(int folderId)
		{
			List<Event> events = conn.Value.Query<Event>("SELECT * FROM Event WHERE Event.FolderId = ?", folderId);
			return events;
		}

		/// <summary>
		/// Gets all events from the specified folder within the specified date range. Does not populate the Tags field.
		/// </summary>
		/// <param name="folderId">Folder ID.</param>
		/// <param name="oldestEpoch">Start date in milliseconds since the unix epoch.</param>
		/// <param name="newestEpoch">End date in milliseconds since the unix epoch.</param>
		/// <returns></returns>
		public List<Event> GetEventsWithoutTagsInFolderByDate(int folderId, long oldestEpoch, long newestEpoch)
		{
			List<Event> events = conn.Value.Query<Event>("SELECT * FROM Event WHERE Event.FolderId = ? AND Date >= ? AND Date <= ?", folderId, oldestEpoch, newestEpoch);
			return events;
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
				eventDict[t.EventId].SetTag(t.Key, t.Value);
		}
		/// <summary>
		/// Returns the number of events in the folder. Non-existent folders contain 0 events.
		/// </summary>
		/// <param name="folderId">ID of the folder.</param>
		/// <returns></returns>
		public long CountEventsInFolder(int folderId)
		{
			return conn.Value.ExecuteScalar<long>("SELECT COUNT(*) FROM Event WHERE Event.FolderId = ?", folderId);
		}
		/// <summary>
		/// Returns true if the event with the specified ID exists.
		/// </summary>
		/// <param name="eventId">Event ID to look up.</param>
		/// <returns></returns>
		public bool EventExists(int eventId)
		{
			return conn.Value.ExecuteScalar<int>("SELECT COUNT(*) FROM Event WHERE EventId = ?", eventId) > 0;
		}
		/// <summary>
		/// Deletes all events with Date lower than the specified value. Returns the number of events that were deleted.
		/// </summary>
		/// <param name="ageCutoff">Events with Date lower than this will be deleted.</param>
		public int DeleteEventsOlderThan(long ageCutoff)
		{
			int eventCount = 0;
			conn.Value.RunInTransaction(() =>
			{
				List<Event> events = conn.Value.Query<Event>("SELECT * FROM Event WHERE Event.Date < ?", ageCutoff);
				eventCount = events.Count;
				if (!DeleteEvents(events.Select(e => e.EventId).ToArray()))
					throw new Exception("Unable to delete all " + events.Count + " events");
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
					conn.Value.Execute("UPDATE Event SET HashValue = ? WHERE EventId = ?", ev.HashValue, ev.EventId);
				}
			}
		}
		#endregion
		#region Folder Management
		/// <summary>
		/// Gets a flat list of all folders.
		/// </summary>
		/// <returns></returns>
		public List<Folder> GetAllFolders()
		{
			List<Folder> folders = conn.Value.Query<Folder>("SELECT * FROM Folder");
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
			conn.Value.RunInTransaction(() =>
			{
				FolderStructure root = GetFolderStructure();
				if (root.TryGetNode(parentFolderId, out FolderStructure parent))
				{
					if (parent.GetChild(folderName) == null)
					{
						nf = new Folder(folderName, parentFolderId);
						conn.Value.Insert(nf);
					}
					else
						eMsg = "A folder with this name already exists in the specified location.";
				}
				else
					eMsg = "Parent folder (ID: " + parentFolderId + ") was not found.";
			});
			if (eMsg == null)
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
			conn.Value.RunInTransaction(() =>
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
										conn.Value.Execute("UPDATE Folder SET ParentFolderId = ? WHERE FolderId = ?", newParentFolderId, folderId);
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
			conn.Value.RunInTransaction(() =>
			{
				FolderStructure root = GetFolderStructure();
				if (root.TryGetNode(folderId, out FolderStructure current))
				{
					if (current.Parent.GetChild(newFolderName) == null)
						conn.Value.Execute("UPDATE Folder SET Name = ? WHERE FolderId = ?", newFolderName, folderId);
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
			conn.Value.RunInTransaction(() =>
			{
				FolderStructure root = GetFolderStructure();
				if (root.TryGetNode(folderId, out FolderStructure current))
				{
					if (current.Children.Count == 0)
					{
						long eventCount = CountEventsInFolder(folderId);
						if (eventCount == 0)
							conn.Value.Delete<Folder>(folderId);
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
			List<Folder> folders = conn.Value.Query<Folder>("SELECT * FROM Folder WHERE folderId = ?", folderId);
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
			conn.Value.RunInTransaction(() =>
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
			conn.Value.RunInTransaction(() =>
			{
				filters = conn.Value.Query<Filter>("SELECT * FROM Filter");
				conditions = conn.Value.Query<FilterItemCount>("SELECT FilterId, COUNT(FilterConditionId) as Count FROM FilterCondition WHERE Enabled = 1 GROUP BY FilterId");
				actions = conn.Value.Query<FilterItemCount>("SELECT FilterId, COUNT(FilterActionId) as Count FROM FilterAction WHERE Enabled = 1 GROUP BY FilterId");
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
					.OrderBy(f => f.filter.Order)
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
			conn.Value.RunInTransaction(() =>
			{
				if (onlyEnabledFilters)
					filters = conn.Value.Query<Filter>("SELECT * FROM Filter WHERE Enabled = ?", true);
				else
					filters = conn.Value.Query<Filter>("SELECT * FROM Filter");
				conditions = conn.Value.Query<FilterCondition>("SELECT * FROM FilterCondition");
				actions = conn.Value.Query<FilterAction>("SELECT * FROM FilterAction");
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
					.OrderBy(f => f.filter.Order)
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
			conn.Value.RunInTransaction(() =>
			{
				filters = conn.Value.Query<Filter>("SELECT * FROM Filter WHERE FilterId = ?", filterId);
				if (filters.Count == 1)
				{
					conditions = conn.Value.Query<FilterCondition>("SELECT * FROM FilterCondition WHERE FilterId = ?", filterId);
					actions = conn.Value.Query<FilterAction>("SELECT * FROM FilterAction WHERE FilterId = ?", filterId);
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
		/// Tries to add the specified filter to the database, returning true if successful or false if an error occurred (see errorMessage out param).
		/// </summary>
		/// <param name="newFilter">Filter to add.  The Name field must be set before passing it in here.  The FilterId field will be set upon successful addition to the database.</param>
		/// <param name="errorMessage">Error message will be set if this method returns false.</param>
		/// <returns></returns>
		public bool AddFilter(Filter newFilter, out string errorMessage)
		{
			if (!StringUtil.IsPrintableName(newFilter?.Name))
			{
				errorMessage = "Invalid filter name. Name must be entirely ASCII-printable (ASCII [32-126]) and contain at least one alphanumeric character.";
				return false;
			}
			newFilter.Name = newFilter.Name.Trim();
			string emsg = null;
			conn.Value.RunInTransaction(() =>
			{
				try
				{
					List<Filter> existingFilters = conn.Value.Query<Filter>("SELECT * FROM Filter WHERE Name = ?", newFilter.Name);
					if (existingFilters.Count > 0)
						emsg = "A filter already exists with that name.";
					else
						conn.Value.Insert(newFilter);
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
			conn.Value.RunInTransaction(() =>
			{
				List<Filter> existing = conn.Value.Query<Filter>("SELECT * FROM Filter WHERE FilterId = ?", full.filter.FilterId);
				if (existing.Count == 0)
					emsg = "Update Failed.  Unable to find filter with ID " + full.filter.FilterId + ".";
				else
				{
					if (conn.Value.Update(full.filter) == 0)
					{
						emsg = "Failed to update filter with ID " + full.filter.FilterId + " for an unknown reason.";
					}
					else
					{
						conn.Value.UpdateAll(full.conditions, false);
						conn.Value.UpdateAll(full.actions, false);
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
			conn.Value.RunInTransaction(() =>
			{
				affectedRows += conn.Value.Execute("DELETE FROM Filter WHERE FilterId = ?", filterId);
				affectedRows += conn.Value.Execute("DELETE FROM FilterCondition WHERE FilterId = ?", filterId);
				affectedRows += conn.Value.Execute("DELETE FROM FilterAction WHERE FilterId = ?", filterId);
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
			return conn.Value.ExecuteScalar<int>("SELECT COUNT(*) FROM Filter WHERE FilterId = ?", filterId) > 0;
		}

		/// <summary>
		/// Sets the Order field as specified for each of the listed filters.
		/// </summary>
		/// <param name="newOrder">An array specifying the Order to assign to each FilterId.</param>
		public void ReorderFilters(FilterOrder[] newOrder)
		{
			if (newOrder.Length == 0)
				return; // true;
			int affectedRows = 0;
			conn.Value.RunInTransaction(() =>
			{
				foreach (FilterOrder orderItem in newOrder)
					affectedRows += conn.Value.Execute("UPDATE Filter SET [Order] = ? WHERE FilterId = ?", orderItem.Order, orderItem.FilterId);
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
			conn.Value.RunInTransaction(() =>
			{
				if (!FilterExists(filterCondition.FilterId))
					emsg = "Could not find filter with ID " + filterCondition.FilterId;
				else
					conn.Value.Insert(filterCondition);
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
			conn.Value.RunInTransaction(() =>
			{
				if (!FilterExists(filterCondition.FilterId))
					emsg = "Could not find filter with ID " + filterCondition.FilterId;
				else
					conn.Value.Update(filterCondition);
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
			int rowsUpdated = conn.Value.Delete<FilterCondition>(filterConditionId);
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
			conn.Value.RunInTransaction(() =>
			{
				if (!FilterExists(filterAction.FilterId))
					emsg = "Could not find filter with ID " + filterAction.FilterId;
				else
					conn.Value.Insert(filterAction);
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
			conn.Value.RunInTransaction(() =>
			{
				if (!FilterExists(filterAction.FilterId))
					emsg = "Could not find filter with ID " + filterAction.FilterId;
				else
					conn.Value.Update(filterAction);
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
			int rowsUpdated = conn.Value.Delete<FilterAction>(filterActionId);
			return rowsUpdated == 1;
		}
		#endregion

		#region IDisposable
		private bool disposedValue;
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// dispose managed state (managed objects)
					if (conn.IsValueCreated)
						conn.Value.Dispose();
				}

				// free unmanaged resources (unmanaged objects) and override finalizer
				// set large fields to null
				disposedValue = true;
			}
		}

		// override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~DB()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}

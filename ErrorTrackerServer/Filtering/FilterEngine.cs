using BPUtil;
using ErrorTrackerServer.Controllers;
using ErrorTrackerServer.Database.Project.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ErrorTrackerServer.Filtering
{
	/// <summary>
	/// Provides access to the filtering engine.  Disposable (use in using block) and not thread-safe (each thread should make its own FilterEngine instance).
	/// </summary>
	public class FilterEngine : IDisposable
	{
		public readonly string ProjectName;
		private bool disposedValue;
		/// <summary>
		/// A reference to the FilterEngine's db object, especially for use by the Submit controller.
		/// </summary>
		public DB db { get; private set; }
		private Lazy<FolderStructure> folderStructure;
		private DeferredActionCollection deferredActions = new DeferredActionCollection();
		public FilterEngine(string projectName)
		{
			ProjectName = projectName;
			db = new DB(projectName);
			folderStructure = new Lazy<FolderStructure>(() =>
			{
				using (DB tmpDb = new DB(projectName))
					return tmpDb.GetFolderStructure();
			}, false);
		}
		/// <summary>
		/// Runs a filter on all events in the specified folder.
		/// </summary>
		/// <param name="filterId">ID of the filter.</param>
		/// <param name="folderId">ID of the folder to run the filter on.</param>
		/// <param name="runIfDisabled">If true, the filter will be run even if the filter is disabled.</param>
		public void RunFilterAgainstFolder(int filterId, int folderId, bool runIfDisabled = false)
		{
			FullFilter full = db.GetFilter(filterId);
			if (full == null)
				throw new Exception("Failed to find filter with ID " + filterId);
			RunFilterAgainstFolder(full, folderId, runIfDisabled);
		}

		/// <summary>
		/// Runs a filter on all events in the specified folder.
		/// </summary>
		/// <param name="full">Filter to run.</param>
		/// <param name="folderId">ID of the folder to run the filter on.</param>
		/// <param name="runIfDisabled">If true, the filter will be run even if the filter is disabled.</param>
		public void RunFilterAgainstFolder(FullFilter full, int folderId, bool runIfDisabled = false)
		{
			if (!full.filter.Enabled && !runIfDisabled)
				return; // Filter is disabled and we aren't being told to run it anyway

			if (!full.actions.Any(a => a.Enabled))
				return; // No actions are enabled, so no need to run this filter.

			if (full.filter.ConditionHandling != ConditionHandling.Unconditional && !full.conditions.Any(c => c.Enabled))
				return; // No enabled conditions, and this is not an unconditional filter

			foreach (Event e in db.GetEventsInFolderDeferred(folderId, null))
			{
				DeferredRunFilterAgainstEvent(full, e, false); // This method can indicate to stop executing filters against the event, but we are already done either way.
			}
			deferredActions.ExecuteDeferredActions(db);
		}
		/// <summary>
		/// Runs enabled filters on all events in the specified folder.
		/// </summary>
		/// <param name="folderId"></param>
		public void RunEnabledFiltersAgainstFolder(int folderId)
		{
			List<FullFilter> enabledFilters = db.GetFilters(true);
			foreach (Event e in db.GetEventsInFolderDeferred(folderId, null))
			{
				foreach (FullFilter full in enabledFilters)
				{
					if (DeferredRunFilterAgainstEvent(full, e, false))
						break;
				}
			}
			deferredActions.ExecuteDeferredActions(db);
		}
		/// <summary>
		/// Runs a filter against all events. This method is not fast, and should be used sparingly.
		/// </summary>
		/// <param name="filterId">ID of the filter.</param>
		/// <param name="runIfDisabled">If true, the filter will be run even if the filter is disabled.</param>
		public void RunFilterAgainstAllEvents(int filterId, bool runIfDisabled = false)
		{
			FullFilter full = db.GetFilter(filterId);
			if (full == null)
				throw new Exception("Failed to find filter with ID " + filterId);
			RunFilterAgainstAllEvents(full, runIfDisabled);
		}

		/// <summary>
		/// Runs a filter against all events. This method is not fast, and should be used sparingly.
		/// </summary>
		/// <param name="full">Filter to run.</param>
		/// <param name="runIfDisabled">If true, the filter will be run even if the filter is disabled.</param>
		public void RunFilterAgainstAllEvents(FullFilter full, bool runIfDisabled = false)
		{
			if (!full.filter.Enabled && !runIfDisabled)
				return; // Filter is disabled and we aren't being told to run it anyway

			if (!full.actions.Any(a => a.Enabled))
				return; // No actions are enabled, so no need to run this filter.

			if (full.filter.ConditionHandling != ConditionHandling.Unconditional && !full.conditions.Any(c => c.Enabled))
				return; // No enabled conditions, and this is not an unconditional filter

			foreach (Event e in db.GetAllEventsDeferred(null))
			{
				DeferredRunFilterAgainstEvent(full, e, false); // This method can indicate to stop executing filters against the event, but we are already done either way.
			}
			deferredActions.ExecuteDeferredActions(db);
		}

		/// <summary>
		/// Runs all enabled filters against all events. This method is not fast, and should be used sparingly.
		/// </summary>
		public void RunEnabledFiltersAgainstAllEvents()
		{
			List<FullFilter> enabledFilters = db.GetFilters(true);
			foreach (Event e in db.GetAllEventsDeferred(null))
			{
				foreach (FullFilter full in enabledFilters)
				{
					if (DeferredRunFilterAgainstEvent(full, e, false))
						break;
				}
			}
			deferredActions.ExecuteDeferredActions(db);
		}

		/// <summary>
		/// Runs all enabled filters against the specified event.
		/// </summary>
		/// <param name="eventId">ID of the event to run filters against.</param>
		public void RunEnabledFiltersAgainstEvent(int eventId)
		{
			Event e = db.GetEvent(eventId);
			if (e == null)
			{
				Logger.Debug("FilterEngine was asked to run enabled filters against a null event (ID " + eventId + ").");
				return;
			}
			RunEnabledFiltersAgainstEvent(e, false);
		}
		/// <summary>
		/// Checks for duplicates, adds the event to the database, and runs all enabled filters against it, within a single transaction.
		/// </summary>
		/// <param name="ev">The event to run filters against.</param>
		/// <returns>A BasicEventTimer containing timing data.</returns>
		public BasicEventTimer AddEventAndRunEnabledFilters(Event ev)
		{
			BasicEventTimer bet = new BasicEventTimer();
			if (ev == null)
				throw new ArgumentNullException("ev", "FilterEngine.AddEventAndRunEnabledFilters was given a null event.");
			try
			{
				bet.Start("Begin Transaction");
				db.RunInTransaction(() =>
				{
					try
					{
						bet.Start("Dupe Check");
						// If our response is not received by the client, they will most likely submit again, causing a duplicate to be received.
						// Check for duplicate submissions.
						List<Event> events = db.GetEventsByDate(ev.Date, ev.Date);
						bool anyDupe = events.Any(existing =>
						{
							if (existing.Date == ev.Date
							&& existing.EventType == ev.EventType
							&& existing.SubType == ev.SubType
							&& existing.Message == ev.Message
							&& existing.GetTagCount() == ev.GetTagCount())
							{
								// All else is the same. Compare tags.
								List<Tag> existingTags = existing.GetAllTags();
								existingTags.Sort(CompareTags);

								List<Tag> newTags = ev.GetAllTags();
								newTags.Sort(CompareTags);

								for (int i = 0; i < existingTags.Count; i++)
									if (existingTags[i].Key != newTags[i].Key || existingTags[i].Value != newTags[i].Value)
										return false;

								return true;
							}
							return false;
						});

						// Skip adding the event if it is a duplicate.
						if (anyDupe)
						{
							bet.Start("Duplicate Found");
							bet.Stop();
							return;
						}

						// Add the event to the database
						bet.Start("Insert");
						db.AddEvent(ev);

						if (Settings.data.verboseSubmitLogging)
							Util.SubmitLog(ProjectName, "Event " + ev.EventId + " Inserted");

						// Run Filters
						bet.Start("Filter");
						RunEnabledFiltersAgainstEvent(ev, true);

						bet.Start("Commit Transaction");
					}
					catch
					{
						bet.Start("Rollback Transaction");
						throw;
					}
				});
				bet.Stop();
			}
			catch (Exception ex)
			{
				bet.Stop();
				throw new FilterException(bet, "An exception was thrown while adding and filtering a new event.", ex);
			}
			return bet;
		}

		private static int CompareTags(Tag a, Tag b)
		{
			int diff = a.Key.CompareTo(b.Key);
			if (diff == 0)
				diff = a.Value.CompareTo(b.Value);
			return diff;
		}

		/// <summary>
		/// Runs all enabled filters against the specified event.
		/// </summary>
		/// <param name="e">The event to run filters against. Must have tags already loaded!</param>
		/// <param name="isEventSubmission">Pass true if the current operation is an event submission and this is the automatic filtering run. Additional logging may be performed for debugging purposes.</param>
		private void RunEnabledFiltersAgainstEvent(Event e, bool isEventSubmission)
		{
			if (e == null)
			{
				Logger.Debug("FilterEngine was asked to run enabled filters against a null event (ID unknown).");
				return;
			}
			List<FullFilter> enabledFilters = db.GetFilters(true);
			if (isEventSubmission && Settings.data.verboseSubmitLogging)
				Util.SubmitLog(ProjectName, "Event " + e.EventId + " Running " + enabledFilters.Count + " Filters");
			foreach (FullFilter full in enabledFilters)
			{
				if (DeferredRunFilterAgainstEvent(full, e, isEventSubmission))
					break;
			}
			deferredActions.ExecuteDeferredActions(db, isEventSubmission);
		}

		/// <summary>
		/// <para>Runs the filter against the event (whether the filter is enabled or not).  Returns true if filter execution against this event should cease immediately.  This method logs exceptions and does not rethrow them.</para>
		/// <para>AFTER CALLING THIS METHOD ONE OR MORE TIMES, CALL deferredActions.ExecuteDeferredActions()</para>
		/// </summary>
		/// <param name="full">A filter to run against the event.</param>
		/// <param name="e">An event with the Tags field populated.</param>
		/// <param name="isEventSubmission">Pass true if the current operation is an event submission and this is the automatic filtering run. Additional logging may be performed for debugging purposes.</param>
		/// <returns></returns>
		private bool DeferredRunFilterAgainstEvent(FullFilter full, Event e, bool isEventSubmission)
		{
			try
			{
				// Evaluate Conditions
				bool conditionsMet = false;
				if (full.filter.ConditionHandling == ConditionHandling.Unconditional)
				{
					conditionsMet = true;
					if (conditionsMet && isEventSubmission && Settings.data.verboseSubmitLogging)
						Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + full.filter.FilterId + " met unconditionally. " + full.actions.Count(a => a.Enabled) + "/" + full.actions.Length + " actions enabled.");
				}
				else if (full.filter.ConditionHandling == ConditionHandling.All)
				{
					int metCount = 0;
					int triedCount = 0;
					foreach (FilterCondition condition in full.conditions)
					{
						if (condition.Enabled)
						{
							triedCount++;
							if (EvalCondition(condition, e, isEventSubmission))
								metCount++;
							else
								break;
						}
					}
					conditionsMet = metCount > 0 && metCount == triedCount;
					if (conditionsMet && isEventSubmission && Settings.data.verboseSubmitLogging)
						Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + full.filter.FilterId + " met " + metCount + "/" + triedCount + " conditions. " + full.actions.Count(a => a.Enabled) + "/" + full.actions.Length + " actions enabled.");
				}
				else if (full.filter.ConditionHandling == ConditionHandling.Any)
				{
					foreach (FilterCondition condition in full.conditions)
					{
						if (condition.Enabled)
						{
							if (EvalCondition(condition, e, isEventSubmission))
							{
								conditionsMet = true;
								break;
							}
						}
					}
					if (conditionsMet && isEventSubmission && Settings.data.verboseSubmitLogging)
						Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + full.filter.FilterId + " met any one condition. " + full.actions.Count(a => a.Enabled) + "/" + full.actions.Length + " actions enabled.");
				}
				else
					throw new Exception("[Filter " + full.filter.FilterId + "] Unsupported filter ConditionHandling: " + full.filter.ConditionHandling);

				// Execute Actions if Conditions were sufficiently met.
				if (conditionsMet)
				{
					foreach (FilterAction action in full.actions)
					{
						try
						{
							if (DeferredExecAction(action, e, isEventSubmission))
								return true;
						}
						catch (Exception ex)
						{
							Logger.Debug(ex);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
			return false;
		}
		/// <summary>
		/// Evaluates the specified filter condition against the event, returning true if the condition is met.
		/// </summary>
		/// <param name="condition">Condition</param>
		/// <param name="e">Event</param>
		/// <param name="isEventSubmission">Pass true if the current operation is an event submission and this is the automatic filtering run. Additional logging may be performed for debugging purposes.</param>
		/// <returns></returns>
		private bool EvalCondition(FilterCondition condition, Event e, bool isEventSubmission)
		{
			if (!condition.Enabled)
				throw new Exception("Disabled conditions cannot be evaluated.");
			if (string.IsNullOrWhiteSpace(condition.TagKey))
				return false;

			string keyLower = condition.TagKey.ToLower();
			string valueToTest = null;
			if (keyLower == "message")
				valueToTest = e.Message;
			else if (keyLower == "eventtype")
				valueToTest = e.EventType.ToString();
			else if (keyLower == "subtype")
				valueToTest = e.SubType;
			else if (keyLower == "date")
				valueToTest = TimeUtil.DateTimeFromEpochMS(e.Date).ToString("yyyy/MM/dd hh/mm/ss tt");
			else if (keyLower == "folder")
			{
				if (folderStructure.Value.TryGetNode(e.FolderId, out FolderStructure eventFolder))
					valueToTest = eventFolder.AbsolutePath;
				else
					valueToTest = null;
			}
			else if (keyLower == "color")
				valueToTest = e.Color.ToString("X").PadLeft(8, '0').Substring(2); // Converts to hex color value (6 chars)
			else
			{
				if (e.TryGetTag(keyLower, out string value))
					valueToTest = value;
			}

			bool result;

			if (condition.Regex)
				result = DoRegexOperation(condition, valueToTest);
			else
				result = DoPlaintextOperation(condition, valueToTest);

			if (condition.Invert)
				result = !result;

			if (result && isEventSubmission && Settings.data.verboseSubmitLogging)
				Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + condition.FilterId + " Condition " + condition.FilterConditionId + " met");

			return result;
		}

		private bool DoRegexOperation(FilterCondition condition, string valueToTest)
		{
			if (valueToTest == null)
				valueToTest = "";
			string pattern = condition.Query;
			if (condition.Operator == FilterConditionOperator.Equals)
			{
				if (!pattern.StartsWith("^") && !pattern.EndsWith("$"))
					pattern = "^" + pattern + "$";
				else if (!pattern.StartsWith("^"))
					pattern = "^" + pattern;
				else if (!pattern.EndsWith("$"))
					pattern = pattern + "$";
			}
			else if (condition.Operator == FilterConditionOperator.StartsWith)
			{
				if (!pattern.StartsWith("^"))
					pattern = "^" + pattern;
			}
			else if (condition.Operator == FilterConditionOperator.EndsWith)
			{
				if (!pattern.EndsWith("$"))
					pattern = pattern + "$";
			}
			return Regex.IsMatch(valueToTest, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
		}

		private bool DoPlaintextOperation(FilterCondition condition, string valueToTest)
		{
			if (valueToTest == null)
				valueToTest = "";
			if (condition.Operator == FilterConditionOperator.Contains)
				return valueToTest.IndexOf(condition.Query, StringComparison.OrdinalIgnoreCase) > -1;
			else if (condition.Operator == FilterConditionOperator.Equals)
				return valueToTest.Equals(condition.Query, StringComparison.OrdinalIgnoreCase);
			else if (condition.Operator == FilterConditionOperator.StartsWith)
				return valueToTest.StartsWith(condition.Query, StringComparison.OrdinalIgnoreCase);
			else if (condition.Operator == FilterConditionOperator.EndsWith)
				return valueToTest.EndsWith(condition.Query, StringComparison.OrdinalIgnoreCase);
			else
				throw new Exception("[Filter " + condition.FilterId + "] Unsupported FilterCondition operator \"" + condition.Operator + "\"");
		}

		/// <summary>
		/// Executes the specified filter action on the event.  Returns true if filter execution against this event should cease immediately.
		/// </summary>
		/// <param name="action">Action</param>
		/// <param name="e">Event</param>
		/// <param name="isEventSubmission">Pass true if the current operation is an event submission and this is the automatic filtering run. Additional logging may be performed for debugging purposes.</param>
		/// <returns></returns>
		private bool DeferredExecAction(FilterAction action, Event e, bool isEventSubmission)
		{
			if (action.Enabled)
			{
				if (action.Operator == FilterActionType.MoveTo)
				{
					FolderStructure targetFolder = folderStructure.Value.ResolvePath(action.Argument.Trim(), null);
					if (targetFolder == null)
						targetFolder = db.FolderResolvePath(action.Argument.Trim());
					if (targetFolder != null)
					{
						if (isEventSubmission && Settings.data.verboseSubmitLogging)
						{
							string currentFolderPathLabel = "";
							if (folderStructure.Value.TryGetNode(e.FolderId, out FolderStructure currentFolder))
								currentFolderPathLabel = " (" + currentFolder.AbsolutePath + ")";
							Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + action.FilterId + " Action " + action.FilterActionId + " (MoveTo) deferred move from folder " + e.FolderId + currentFolderPathLabel + " to folder " + targetFolder.FolderId + " (" + targetFolder.AbsolutePath + ")");
						}

						deferredActions.MoveEventTo(e, targetFolder.FolderId);
						// Make change in memory so that later filters during this filtering operation can see and act upon the new value.
						e.FolderId = targetFolder.FolderId;
					}
					else
					{
						Logger.Info("[Filter " + action.FilterId + "] FilterAction " + action.FilterActionId + " was unable to resolve path \"" + action.Argument + "\"");

						if (isEventSubmission && Settings.data.verboseSubmitLogging)
							Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + action.FilterId + " Action " + action.FilterActionId + " (MoveTo) failed: unable to resolve path \"" + action.Argument + "\"");
					}
					return false;
				}
				else if (action.Operator == FilterActionType.Delete)
				{
					if (isEventSubmission && Settings.data.verboseSubmitLogging)
						Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + action.FilterId + " Action " + action.FilterActionId + " (Delete) deferred");

					deferredActions.DeleteEvent(e);
					return true;
				}
				else if (action.Operator == FilterActionType.SetColor)
				{
					uint color = 0;
					try
					{
						string hex = action.Argument;
						if (hex.StartsWith("#"))
							hex = hex.Substring(1);
						color = Convert.ToUInt32(hex, 16);

						if (isEventSubmission && Settings.data.verboseSubmitLogging)
							Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + action.FilterId + " Action " + action.FilterActionId + " (SetColor) deferred set color to #" + hex);
					}
					catch
					{
						Logger.Info("[Filter " + action.FilterId + "] FilterAction " + action.FilterActionId + " with Operator \"SetColor\" has invalid Argument \"" + action.Argument + "\"");

						if (isEventSubmission && Settings.data.verboseSubmitLogging)
							Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + action.FilterId + " Action " + action.FilterActionId + " (SetColor) failed: invalid Argument \"" + action.Argument + "\"");

						return false;
					}
					deferredActions.SetEventColor(e, color);
					// Make change in memory so that later filters during this filtering operation can see and act upon the new value.
					e.Color = color;
					return false;
				}
				else if (action.Operator == FilterActionType.StopExecution)
				{
					if (isEventSubmission && Settings.data.verboseSubmitLogging)
						Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + action.FilterId + " Action " + action.FilterActionId + " (StopExecution)");
					return true;
				}
				else if (action.Operator == FilterActionType.MarkRead)
				{
					if (isEventSubmission && Settings.data.verboseSubmitLogging)
						Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + action.FilterId + " Action " + action.FilterActionId + " (MarkRead) deferred");

					deferredActions.SetReadState(e, true);
					return false;
				}
				else if (action.Operator == FilterActionType.MarkUnread)
				{
					if (isEventSubmission && Settings.data.verboseSubmitLogging)
						Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + action.FilterId + " Action " + action.FilterActionId + " (MarkUnread) deferred");

					deferredActions.SetReadState(e, false);
					return false;
				}
				else
					throw new Exception("Unsupported filter action operator: " + action.Operator);
			}
			return false;
		}

		#region Search Feature
		/// <summary>
		/// Given a search query, returns a list of matching events.  Each returned event will have its Tags populated from the DB if there was no match in the base event fields -- so you may want to populate or depopulate the tags on all these events before serializing to send to the client.
		/// </summary>
		/// <param name="query"></param>
		/// <param name="folderId">Folder ID to search.  If -1, all events are searched.</param>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <returns></returns>
		public List<Event> Search(string query, int folderId, string eventListCustomTagKey)
		{
			List<Event> matches = new List<Event>();
			foreach (Event e in GetEventsForSearchDeferred(folderId, eventListCustomTagKey))
			{
				if (SearchEvent(e, query))
					matches.Add(e);
			}
			return matches;
		}

		/// <summary>
		/// Returns true if the query is found in the event's Message, EventType, Subtype, or any of the Tag values (not case-sensitive).
		/// </summary>
		/// <param name="e"></param>
		/// <param name="query"></param>
		/// <returns></returns>
		private bool SearchEvent(Event e, string query)
		{
			if (StringContains(e.Message, query)
				|| StringContains(e.EventType.ToString(), query)
				|| StringContains(e.SubType, query))
			{
				return true;
			}
			else
			{
				if (e.GetAllTags().Any(t => StringContains(t.Value, query)))
					return true;
			}
			return false;
		}

		private bool StringContains(string str, string query)
		{
			if (str == null)
				return false;
			return str.IndexOf(query, StringComparison.OrdinalIgnoreCase) > -1;
		}

		/// <summary>
		/// Given a set of filter conditions, returns a list of matching events.  The Tags of each event may or may not be populated by this search depending to
		/// </summary>
		/// <param name="conditions">Array of filter conditions to evaluate. The FilterCondition.Enabled field is disregarded.</param>
		/// <param name="matchAll">If true, only one of the conditions needs to match. If false, all conditions need to match.</param>
		/// <param name="folderId">Folder ID to search.  If -1, all events are searched.</param>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <returns></returns>
		public List<Event> AdvancedSearch(FilterCondition[] conditions, bool matchAll, int folderId, string eventListCustomTagKey)
		{
			List<Event> matches = new List<Event>();
			if (conditions == null || conditions.Length == 0)
				return matches;
			foreach (FilterCondition condition in conditions)
				condition.Enabled = true;
			foreach (Event e in GetEventsForSearchDeferred(folderId, eventListCustomTagKey))
			{
				if (!matchAll)
				{
					foreach (FilterCondition condition in conditions)
					{
						if (EvalCondition(condition, e, false))
						{
							matches.Add(e);
							break;
						}
					}
				}
				else
				{
					int metCount = 0;
					foreach (FilterCondition condition in conditions)
					{
						if (EvalCondition(condition, e, false))
							metCount++;
						else
							break;
					}
					if (metCount == conditions.Length)
						matches.Add(e);
				}
			}
			return matches;
		}

		private IEnumerable<Event> GetEventsForSearchDeferred(int folderId, string eventListCustomTagKey)
		{
			if (folderId == -1)
				return db.GetAllEventsDeferred(eventListCustomTagKey);
			else
				return db.GetEventsInFolderDeferred(folderId, eventListCustomTagKey);
		}
		#endregion

		#region IDisposable Pattern
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// dispose managed state (managed objects)
					db?.Dispose();
					db = null;
				}

				// free unmanaged resources (unmanaged objects) and override finalizer
				// set large fields to null
				disposedValue = true;
			}
		}

		// // override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~FilterEngine()
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
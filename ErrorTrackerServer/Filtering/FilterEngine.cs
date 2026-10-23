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
		/// <summary>
		/// Receives the actions this engine decides to take.  Normally <see cref="deferredActions"/>; during a dry run, a recorder supplied by the caller.
		/// </summary>
		private IFilterActionSink sink;
		/// <summary>
		/// If true, this engine never changes the database: actions go to the caller's sink, and MoveTo never creates folders.
		/// </summary>
		public readonly bool DryRun;
		/// <summary>
		/// Maximum time one regular expression condition may spend matching one value.  A condition that times out makes its filter skip the event, as any other error in a condition does.
		/// </summary>
		public static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(250);
		/// <summary>
		/// Number of regular expression evaluations which timed out (see <see cref="RegexMatchTimeout"/>) since this engine was created.
		/// </summary>
		public int RegexTimeoutCount { get; private set; }
		/// <summary>
		/// Number of times a filter failed with an error (other than a regex timeout) while being evaluated against an event, since this engine was created.  Such a filter does not act on that event.
		/// </summary>
		public int FilterErrorCount { get; private set; }
		/// <summary>
		/// Folders which were resolved (and possibly created) via the database because they were not in <see cref="folderStructure"/>, keyed by the MoveTo argument.
		/// </summary>
		private Dictionary<string, FolderStructure> resolvedMoveTargets = new Dictionary<string, FolderStructure>();
		/// <summary>
		/// Counts what this engine's executed actions have done so far.  Always empty for a dry run (the caller's sink does the counting).
		/// </summary>
		public FilterRunEffects Effects { get { return deferredActions.Effects; } }
		public FilterEngine(string projectName) : this(projectName, null) { }
		/// <summary>
		/// Creates a FilterEngine.
		/// </summary>
		/// <param name="projectName">Project name.</param>
		/// <param name="dryRunSink">If not null, the engine operates in dry-run mode: every action is sent to this sink instead of being executed, and no folders are created.</param>
		public FilterEngine(string projectName, IFilterActionSink dryRunSink)
		{
			ProjectName = projectName;
			db = new DB(projectName);
			folderStructure = new Lazy<FolderStructure>(() =>
			{
				using (DB tmpDb = new DB(projectName))
					return tmpDb.GetFolderStructure();
			}, false);
			DryRun = dryRunSink != null;
			sink = DryRun ? dryRunSink : deferredActions;
		}
		/// <summary>
		/// Gets the folder structure as it was when this engine first needed it.  Folders created during a run are not included.
		/// </summary>
		public FolderStructure GetFolderStructure()
		{
			return folderStructure.Value;
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
		public void RunEnabledFiltersAgainstEvent(long eventId)
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
		/// <param name="isDupe">(Output) true if this event was a duplicate of an existing event, and therefore not added to the database.  Some events which happen very rapidly (more than once per millisecond) can be somewhat throttled by this.</param>
		/// <returns>A BasicEventTimer containing timing data.</returns>
		public BasicEventTimer AddEventAndRunEnabledFilters(Event ev, out bool isDupe)
		{
			BasicEventTimer bet = new BasicEventTimer();
			if (ev == null)
				throw new ArgumentNullException("ev", "FilterEngine.AddEventAndRunEnabledFilters was given a null event.");
			bool anyDupe = false;
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
						anyDupe = events.Any(existing =>
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
			isDupe = anyDupe;
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
		/// <para>Runs the given filters, in order, against the event, exactly as incoming events are filtered: processing of the event stops after a Delete or StopExecution action.  Disabled filters in the list are run too, so pass only the filters you want to run.</para>
		/// <para>Returns true if any filter's conditions were met.</para>
		/// <para>Unless this is a dry run, AFTER CALLING THIS METHOD ONE OR MORE TIMES, CALL <see cref="ExecuteDeferredActions"/>.</para>
		/// </summary>
		/// <param name="filters">Filters to run, in execution order.</param>
		/// <param name="e">An event with the Tags field populated.</param>
		/// <returns></returns>
		public bool DeferredRunFiltersAgainstEvent(IList<FullFilter> filters, Event e)
		{
			bool anyMatched = false;
			foreach (FullFilter full in filters)
			{
				FilterEvalOutcome outcome = EvaluateFilterAgainstEvent(full, e, false);
				if (outcome.Matched)
					anyMatched = true;
				if (outcome.StopProcessing)
					break;
			}
			return anyMatched;
		}

		/// <summary>
		/// Executes the actions scheduled by <see cref="DeferredRunFiltersAgainstEvent"/>.  Returns true if all scheduled actions executed successfully.  Does nothing during a dry run.
		/// </summary>
		/// <returns></returns>
		public bool ExecuteDeferredActions()
		{
			if (DryRun)
				return true;
			return deferredActions.ExecuteDeferredActions(db);
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
			return EvaluateFilterAgainstEvent(full, e, isEventSubmission).StopProcessing;
		}

		/// <summary>
		/// <para>Runs the filter against the event (whether the filter is enabled or not), sending any actions to this engine's sink.  This method logs exceptions and does not rethrow them.</para>
		/// <para>This is the one place where filter conditions and actions are evaluated, for incoming events, filter runs and dry runs alike, so that a dry run predicts exactly what a real run does.</para>
		/// <para>Unless this is a dry run, AFTER CALLING THIS METHOD ONE OR MORE TIMES, CALL deferredActions.ExecuteDeferredActions()</para>
		/// </summary>
		/// <param name="full">A filter to run against the event.</param>
		/// <param name="e">An event with the Tags field populated.  Its FolderId and Color are updated in memory by MoveTo and SetColor actions so that later filters see the new values.</param>
		/// <param name="isEventSubmission">Pass true if the current operation is an event submission and this is the automatic filtering run. Additional logging may be performed for debugging purposes.</param>
		/// <returns></returns>
		public FilterEvalOutcome EvaluateFilterAgainstEvent(FullFilter full, Event e, bool isEventSubmission)
		{
			FilterEvalOutcome outcome = new FilterEvalOutcome();
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
					outcome.Matched = true;
					if (full.actions.Length > 0)
					{
						// Remember that this filter applied actions to the event.
						DeferredExecAction(new FilterAction() { FilterId = full.filter.FilterId, Enabled = true, Operator = FilterActionType.RememberFilterApplied }, e, isEventSubmission);

						// Apply actions configured in the filter.
						foreach (FilterAction action in full.actions)
						{
							try
							{
								if (DeferredExecAction(action, e, isEventSubmission))
								{
									outcome.StopProcessing = true;
									return outcome;
								}
							}
							catch (Exception ex)
							{
								FilterErrorCount++;
								Logger.Debug(ex);
							}
						}
					}
				}
			}
			catch (RegexMatchTimeoutException)
			{
				// Not logged, because one slow pattern could otherwise write a log entry for every event in a large run.
				RegexTimeoutCount++;
				outcome.Errored = true;
			}
			catch (Exception ex)
			{
				FilterErrorCount++;
				outcome.Errored = true;
				Logger.Debug(ex);
			}
			return outcome;
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
				valueToTest = TimeUtil.DateTimeFromEpochMS(e.Date).ToString("yyyy/MM/dd hh:mm:ss tt");
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
			return Regex.IsMatch(valueToTest, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase, RegexMatchTimeout);
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
					string path = action.Argument.Trim();
					FolderStructure targetFolder = folderStructure.Value.ResolvePath(path, null);
					if (targetFolder == null)
					{
						if (DryRun)
						{
							// The folder doesn't exist.  A real run would create it; a dry run must not.
							sink.MoveEventTo(e, -1, GetAbsolutePathForMissingFolder(path));
							// A real run moves the event to the new folder, which isn't in the cached folder structure, so later
							// "Folder" conditions see no path at all.  FolderId -1 reproduces that.
							e.FolderId = -1;
							return false;
						}
						if (!resolvedMoveTargets.TryGetValue(path, out targetFolder))
						{
							bool existedBefore;
							using (DB tmpDb = new DB(ProjectName))
								existedBefore = tmpDb.GetFolderStructure().ResolvePath(path, null) != null;
							targetFolder = db.FolderResolvePath(path);
							if (targetFolder != null)
							{
								resolvedMoveTargets[path] = targetFolder;
								if (!existedBefore)
									deferredActions.Effects.FoldersCreated.Add(targetFolder.AbsolutePath);
							}
						}
					}
					if (targetFolder != null)
					{
						if (isEventSubmission && Settings.data.verboseSubmitLogging)
						{
							string currentFolderPathLabel = "";
							if (folderStructure.Value.TryGetNode(e.FolderId, out FolderStructure currentFolder))
								currentFolderPathLabel = " (" + currentFolder.AbsolutePath + ")";
							Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + action.FilterId + " Action " + action.FilterActionId + " (MoveTo) deferred move from folder " + e.FolderId + currentFolderPathLabel + " to folder " + targetFolder.FolderId + " (" + targetFolder.AbsolutePath + ")");
						}

						sink.MoveEventTo(e, targetFolder.FolderId, targetFolder.AbsolutePath);
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

					sink.DeleteEvent(e);
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
					sink.SetEventColor(e, color);
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

					sink.SetReadState(e, true);
					return false;
				}
				else if (action.Operator == FilterActionType.MarkUnread)
				{
					if (isEventSubmission && Settings.data.verboseSubmitLogging)
						Util.SubmitLog(ProjectName, "Event " + e.EventId + " Filter " + action.FilterId + " Action " + action.FilterActionId + " (MarkUnread) deferred");

					sink.SetReadState(e, false);
					return false;
				}
				else if (action.Operator == FilterActionType.RememberFilterApplied)
				{
					sink.RememberFilterApplied(e, action.FilterId, TimeUtil.GetTimeInMsSinceEpoch());
					return false;
				}
				else
					throw new Exception("Unsupported filter action operator: " + action.Operator);
			}
			return false;
		}

		/// <summary>
		/// Returns the absolute path that a MoveTo argument refers to when the destination folder does not exist yet, as <see cref="FolderStructure.ResolvePath"/> would create it relative to the root.
		/// </summary>
		/// <param name="path">MoveTo argument (trimmed).</param>
		/// <returns></returns>
		private string GetAbsolutePathForMissingFolder(string path)
		{
			// Resolve the longest existing prefix so that ".." and the existing folders' capitalization are handled like ResolvePath does.
			List<string> parts = path.Split('/').ToList();
			if (parts.Count > 0 && parts[0] == "")
				parts.RemoveAt(0);
			FolderStructure current = folderStructure.Value;
			int i = 0;
			for (; i < parts.Count; i++)
			{
				string part = parts[i];
				if (part == "" && i == parts.Count - 1)
					break; // trailing slash
				FolderStructure next = part == ".." ? (current.Parent ?? current) : current.GetChild(part);
				if (next == null)
					break;
				current = next;
			}
			string prefix = current.AbsolutePath.TrimEnd('/');
			string rest = string.Join("/", parts.Skip(i).Where(p => p != ""));
			return prefix + "/" + rest;
		}

		#region Search Feature
		/// <summary>
		/// Given a search query, returns a list of matching events.  Each returned event will have its Tags populated from the DB if there was no match in the base event fields -- so you may want to populate or depopulate the tags on all these events before serializing to send to the client.
		/// Uses clientside matching, so this is slow.
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
		/// Given a set of filter conditions, returns a list of matching events.  The Tags of each event may or may not be populated by this search depending on the requirements of the conditions.
		/// Uses clientside matching, so this is slow.
		/// </summary>
		/// <param name="conditions">Array of filter conditions to evaluate. Disabled conditions are disregarded.</param>
		/// <param name="matchAll">If true, all conditions need to match. If false, only one of the conditions needs to match.</param>
		/// <param name="folderId">Folder ID to search.  If -1, all events are searched.</param>
		/// <param name="eventListCustomTagKey">Custom tag key which a user may have set to include in event summaries.</param>
		/// <returns></returns>
		public List<Event> AdvancedSearch(FilterCondition[] conditions, bool matchAll, int folderId, string eventListCustomTagKey)
		{
			List<Event> matches = new List<Event>();
			if (conditions == null)
				return matches;
			conditions = conditions.Where(c => c.Enabled).ToArray();
			if (conditions.Length == 0)
				return matches;
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
	/// <summary>
	/// The result of evaluating one filter against one event.
	/// </summary>
	public class FilterEvalOutcome
	{
		/// <summary>
		/// True if the filter's conditions were met (its actions were then taken, if it has any).
		/// </summary>
		public bool Matched;
		/// <summary>
		/// True if a Delete or StopExecution action ended processing of the event, so no later filters run on it.
		/// </summary>
		public bool StopProcessing;
		/// <summary>
		/// True if evaluating the filter's conditions failed (for example, a regular expression that is invalid or timed out).  The filter takes no action on the event in that case.
		/// </summary>
		public bool Errored;
	}
}
using BPUtil;
using ErrorTrackerServer.Database.Model;
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
		public FilterEngine(string projectName)
		{
			ProjectName = projectName;
			db = new DB(projectName);
			folderStructure = new Lazy<FolderStructure>(() =>
			{
				return db.GetFolderStructure();
			}, false);
		}
		/// <summary>
		/// Runs a filter on the specified folder.
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
		/// Runs a filter on the specified folder.
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

			List<Event> events = db.GetEventsInFolder(folderId);
			foreach (Event e in events)
				RunFilterAgainstEvent(full, e); // This method can indicate to stop executing filters against the event, but we are already done either way.
		}

		public void RunEnabledFiltersAgainstEvent(int eventId)
		{
			Event e = db.GetEvent(eventId);
			if (e == null)
			{
				Logger.Debug("FilterEngine was asked to run enabled filters against a null event (ID " + eventId + ").");
				return;
			}
			RunEnabledFiltersAgainstEvent(e);
		}
		public void RunEnabledFiltersAgainstEvent(Event e)
		{
			if (e == null)
			{
				Logger.Debug("FilterEngine was asked to run enabled filters against a null event (ID unknown).");
				return;
			}
			List<FullFilter> allFilters = db.GetAllFilters();
			foreach (FullFilter full in allFilters)
			{
				if (RunFilterAgainstEvent(full, e))
					return;
			}
		}

		/// <summary>
		/// Runs the filter against the event (whether the filter is enabled or not).  Returns true if filter execution against this event should cease immediately.
		/// </summary>
		/// <param name="full">A filter to run against the event.</param>
		/// <param name="e">An event with the Tags field populated.</param>
		/// <returns></returns>
		private bool RunFilterAgainstEvent(FullFilter full, Event e)
		{
			// Evaluate Conditions
			bool conditionsMet = false;
			if (full.filter.ConditionHandling == ConditionHandling.Unconditional)
			{
				conditionsMet = true;
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
						if (EvalCondition(condition, e))
							metCount++;
						else
							break;
					}
				}
				conditionsMet = metCount == triedCount;
			}
			else if (full.filter.ConditionHandling == ConditionHandling.Any)
			{
				foreach (FilterCondition condition in full.conditions)
				{
					if (condition.Enabled)
					{
						if (EvalCondition(condition, e))
						{
							conditionsMet = true;
							break;
						}
					}
				}
			}
			else
				throw new Exception("[Filter " + full.filter.FilterId + "] Unsupported filter ConditionHandling: " + full.filter.ConditionHandling);

			// Execute Actions if Conditions were sufficiently met.
			if (conditionsMet)
			{
				foreach (FilterAction action in full.actions)
				{
					if (ExecAction(action, e))
						return true;
				}
			}
			return false;
		}
		/// <summary>
		/// Evaluates the specified filter condition against the event, returning true if the condition is met.
		/// </summary>
		/// <param name="condition">Condition</param>
		/// <param name="e">Event</param>
		/// <returns></returns>
		private bool EvalCondition(FilterCondition condition, Event e)
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

			if (condition.Not)
				result = !result;

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
		/// <returns></returns>
		private bool ExecAction(FilterAction action, Event e)
		{
			if (action.Enabled)
			{
				if (action.Operator == FilterActionType.MoveTo)
				{
					FolderStructure targetFolder = folderStructure.Value.ResolvePath(action.Argument.Trim());
					if (targetFolder == null)
						Logger.Info("[Filter " + action.FilterId + "] FilterAction " + action.FilterActionId + " was unable to resolve path \"" + action.Argument + "\"");
					if (!db.MoveEvents(new long[] { e.EventId }, targetFolder.FolderId))
						Logger.Info("[Filter " + action.FilterId + "] FilterAction " + action.FilterActionId + " failed to move event " + e.EventId + " to \"" + action.Argument + "\"");
					e.FolderId = targetFolder.FolderId;
					return false;
				}
				else if (action.Operator == FilterActionType.Delete)
				{
					if (!db.DeleteEvents(new long[] { e.EventId }))
						Logger.Info("[Filter " + action.FilterId + "] FilterAction " + action.FilterActionId + " failed to delete event " + e.EventId);
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
					}
					catch
					{
						Logger.Info("[Filter " + action.FilterId + "] FilterAction " + action.FilterActionId + " with Operator \"SetColor\" has invalid Argument \"" + action.Argument + "\"");
						return false;
					}
					db.SetEventsColor(new long[] { e.EventId }, color);
					e.Color = color;
					return false;
				}
				else if (action.Operator == FilterActionType.StopExecution)
				{
					return true;
				}
				else
					throw new Exception("Unsupported filter action operator: " + action.Operator);
			}
			return false;
		}

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
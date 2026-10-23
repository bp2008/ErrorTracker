using BPUtil;
using ErrorTrackerServer.Database.Project.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ErrorTrackerServer.Services
{
	/// <summary>
	/// A complete filter definition, without IDs or internal-only data.
	/// </summary>
	public class FilterDefinition
	{
		public string name;
		public bool enabled;
		public ConditionHandling conditionHandling;
		public List<ConditionDefinition> conditions = new List<ConditionDefinition>();
		public List<ActionDefinition> actions = new List<ActionDefinition>();
	}
	public class ConditionDefinition
	{
		public string tagKey;
		public FilterConditionOperator op;
		public string query;
		public bool regex;
		public bool invert;
		public bool enabled = true;
	}
	public class ActionDefinition
	{
		public FilterActionType type;
		public string argument;
		public bool enabled = true;
	}
	/// <summary>
	/// Where to put a filter in the execution order.
	/// </summary>
	public class FilterPosition
	{
		/// <summary>
		/// "first", "last", "before" or "after".
		/// </summary>
		public string Kind = "last";
		/// <summary>
		/// The filter to insert before or after.
		/// </summary>
		public int RelativeToFilterId;
	}
	/// <summary>
	/// Thrown when a filter's etag no longer matches (someone else changed the filter).
	/// </summary>
	public class FilterConflictException : Exception
	{
		public FullFilter Current;
		public FilterConflictException(FullFilter current) : base("The filter was changed by someone else since you read it.")
		{
			Current = current;
		}
	}
	/// <summary>
	/// Thrown when a filter definition is invalid.
	/// </summary>
	public class FilterValidationException : Exception
	{
		public List<string> Problems;
		public FilterValidationException(List<string> problems) : base("The filter definition is invalid:\n- " + string.Join("\n- ", problems))
		{
			Problems = problems;
		}
	}

	/// <summary>
	/// Reads and writes complete filter definitions.  Every write happens in one transaction, so a half-written filter can never run against incoming events.
	/// </summary>
	public static class FilterService
	{
		public static readonly string[] ReservedConditionKeys = new string[] { "EventType", "SubType", "Message", "Date", "Folder", "Color" };
		private static readonly Regex rxHexColor = new Regex("^#?[0-9a-fA-F]{6}$", RegexOptions.Compiled);

		#region Conversion
		/// <summary>
		/// Converts a stored filter to a definition.  RememberFilterApplied actions (internal-only) are omitted.
		/// </summary>
		public static FilterDefinition ToDefinition(FullFilter full)
		{
			FilterDefinition def = new FilterDefinition();
			def.name = full.filter.Name;
			def.enabled = full.filter.Enabled;
			def.conditionHandling = full.filter.ConditionHandling;
			def.conditions = full.conditions.OrderBy(c => c.FilterConditionId).Select(c => new ConditionDefinition()
			{
				tagKey = c.TagKey,
				op = c.Operator,
				query = c.Query,
				regex = c.Regex,
				invert = c.Invert,
				enabled = c.Enabled
			}).ToList();
			def.actions = full.actions.Where(a => a.Operator != FilterActionType.RememberFilterApplied).OrderBy(a => a.FilterActionId).Select(a => new ActionDefinition()
			{
				type = a.Operator,
				argument = a.Argument,
				enabled = a.Enabled
			}).ToList();
			return def;
		}
		/// <summary>
		/// Builds an unsaved <see cref="FullFilter"/> from a definition (FilterId 0), e.g. for previews.
		/// </summary>
		public static FullFilter ToUnsavedFullFilter(FilterDefinition def)
		{
			FullFilter full = new FullFilter();
			full.filter = new Filter() { Name = def.name, Enabled = def.enabled, ConditionHandling = def.conditionHandling };
			full.conditions = def.conditions.Select((c, i) => ToCondition(c, 0, i + 1)).ToArray();
			full.actions = def.actions.Select((a, i) => ToAction(a, 0, i + 1)).ToArray();
			return full;
		}
		private static FilterCondition ToCondition(ConditionDefinition c, int filterId, int conditionId)
		{
			return new FilterCondition() { FilterConditionId = conditionId, FilterId = filterId, TagKey = c.tagKey?.Trim() ?? "", Operator = c.op, Query = c.query ?? "", Regex = c.regex, Invert = c.invert, Enabled = c.enabled };
		}
		private static FilterAction ToAction(ActionDefinition a, int filterId, int actionId)
		{
			return new FilterAction() { FilterActionId = actionId, FilterId = filterId, Operator = a.type, Argument = NormalizeActionArgument(a), Enabled = a.enabled };
		}
		private static string NormalizeActionArgument(ActionDefinition a)
		{
			string arg = a.argument?.Trim() ?? "";
			if (a.type == FilterActionType.SetColor && arg.Length == 6 && !arg.StartsWith("#"))
				arg = "#" + arg;
			if (a.type == FilterActionType.SetColor)
				arg = arg.ToUpper();
			return arg;
		}
		#endregion

		#region ETag
		/// <summary>
		/// Computes the filter's etag: a hash of its full definition and its execution order value.
		/// </summary>
		public static string ComputeEtag(FullFilter full)
		{
			string canonical = JsonConvert.SerializeObject(new { d = ToDefinition(full), o = full.filter.MyOrder });
			using (SHA256 sha = SHA256.Create())
				return Hex.ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Substring(0, 24).ToLower();
		}
		#endregion

		#region Validation and warnings
		/// <summary>
		/// Returns every problem which prevents the definition from being saved.  An empty list means it is valid.
		/// </summary>
		public static List<string> Validate(FilterDefinition def)
		{
			List<string> problems = new List<string>();
			if (def == null)
			{
				problems.Add("definition is required.");
				return problems;
			}
			if (!StringUtil.IsPrintableName(def.name))
				problems.Add("name must be non-empty, entirely ASCII-printable (ASCII 32-126), and contain at least one letter or digit.");
			if (!Enum.IsDefined(typeof(ConditionHandling), def.conditionHandling))
				problems.Add("conditionHandling must be \"All\", \"Any\" or \"Unconditional\".");
			for (int i = 0; i < def.conditions.Count; i++)
			{
				ConditionDefinition c = def.conditions[i];
				string label = "conditions[" + i + "]";
				if (string.IsNullOrWhiteSpace(c.tagKey))
					problems.Add(label + ".tagKey must not be empty (a condition with an empty tagKey never matches).");
				else if (c.tagKey.Trim().Length > 128)
					problems.Add(label + ".tagKey must not be longer than 128 characters.");
				if (!Enum.IsDefined(typeof(FilterConditionOperator), c.op))
					problems.Add(label + ".operator must be \"Contains\", \"Equals\", \"StartsWith\" or \"EndsWith\".");
				if (c.query == null)
					problems.Add(label + ".query is required.");
				else if (c.regex)
				{
					string err = GetRegexError(c.query);
					if (err != null)
						problems.Add(label + ".query is not a valid .NET regular expression: " + err);
				}
			}
			for (int i = 0; i < def.actions.Count; i++)
			{
				ActionDefinition a = def.actions[i];
				string label = "actions[" + i + "]";
				if (a.type == FilterActionType.RememberFilterApplied || !Enum.IsDefined(typeof(FilterActionType), a.type))
					problems.Add(label + ".type must be \"MoveTo\", \"Delete\", \"SetColor\", \"StopExecution\", \"MarkRead\" or \"MarkUnread\".");
				else if (a.type == FilterActionType.MoveTo)
				{
					string path = a.argument?.Trim();
					if (string.IsNullOrEmpty(path))
						problems.Add(label + ".argument must be a folder path for a MoveTo action, e.g. \"/Noise/Bots\".");
					else
					{
						string[] parts = path.Trim('/').Split('/');
						if (path.Trim('/').Length > 0 && parts.Any(p => !Folder.ValidateName(p.Trim()) && p != ".."))
							problems.Add(label + ".argument \"" + path + "\" contains an empty or invalid folder name.");
					}
				}
				else if (a.type == FilterActionType.SetColor)
				{
					if (a.argument == null || !rxHexColor.IsMatch(a.argument.Trim()))
						problems.Add(label + ".argument must be a hex color like \"#FF8800\" for a SetColor action.");
				}
			}
			return problems;
		}
		/// <summary>
		/// Returns null if the pattern compiles as a .NET regular expression, otherwise the error message.
		/// </summary>
		public static string GetRegexError(string pattern)
		{
			try
			{
				new Regex(pattern ?? "", RegexOptions.Singleline | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
				return null;
			}
			catch (ArgumentException ex)
			{
				return ex.Message;
			}
		}
		/// <summary>
		/// Returns warnings about filter-engine behavior which is easy to get wrong (spec PREV-3), based on the definition alone.
		/// </summary>
		/// <param name="full">The filter.</param>
		/// <param name="folders">The project's current folder structure, to detect MoveTo folders which don't exist.</param>
		public static List<string> GetStaticWarnings(FullFilter full, FolderStructure folders)
		{
			List<string> warnings = new List<string>();
			if (!full.filter.Enabled)
				warnings.Add("The filter is disabled, so it will not run on new events. (run_filters with its filterId still runs it.)");
			FilterCondition[] enabledConditions = full.conditions.Where(c => c.Enabled).ToArray();
			if (full.filter.ConditionHandling == ConditionHandling.All && enabledConditions.Length == 0)
				warnings.Add("conditionHandling is \"All\" but no condition is enabled, so the filter never matches. Use \"Unconditional\" to match every event.");
			if (full.filter.ConditionHandling == ConditionHandling.Any && enabledConditions.Length == 0)
				warnings.Add("conditionHandling is \"Any\" but no condition is enabled, so the filter never matches.");
			if (full.filter.ConditionHandling == ConditionHandling.Unconditional && enabledConditions.Length > 0)
				warnings.Add("conditionHandling is \"Unconditional\", so the conditions are ignored and the filter matches every event.");
			if (full.filter.ConditionHandling != ConditionHandling.Unconditional)
			{
				foreach (FilterCondition c in enabledConditions)
				{
					string key = c.TagKey?.Trim() ?? "";
					if (key.Length == 0)
						warnings.Add("A condition has an empty tagKey and never matches.");
					else if (key.Equals("Date", StringComparison.OrdinalIgnoreCase))
						warnings.Add("A \"Date\" condition compares against a text rendering of the event date such as \"2026/10/23 02:05:09 PM\" (UTC, 12-hour clock), not against a real date. It is rarely what you want.");
					if (c.Regex && GetRegexError(c.Query) != null)
						warnings.Add("The regular expression \"" + Truncate(c.Query, 80) + "\" does not compile, so the filter errors on every event and never matches: " + GetRegexError(c.Query));
					if (c.Invert && EventQueryService.IsTagKey(key))
						warnings.Add("The inverted condition on tag \"" + key + "\" also matches events that do not have that tag at all (a missing tag counts as an empty string).");
				}
			}
			FilterAction[] enabledActions = full.actions.Where(a => a.Enabled && a.Operator != FilterActionType.RememberFilterApplied).ToArray();
			if (enabledActions.Length == 0)
				warnings.Add("The filter has no enabled actions, so running it does nothing (except record that it matched, for new events).");
			bool stopped = false;
			foreach (FilterAction a in enabledActions)
			{
				if (stopped)
				{
					warnings.Add("The " + a.Operator + " action comes after a Delete or StopExecution action, so it never runs.");
					continue;
				}
				switch (a.Operator)
				{
					case FilterActionType.MoveTo:
						if (folders != null)
						{
							FolderStructure target = null;
							try { target = folders.ResolvePath(a.Argument?.Trim(), null); }
							catch (Exception ex) { warnings.Add("The MoveTo path \"" + a.Argument + "\" is invalid: " + ex.Message); break; }
							if (target == null)
								warnings.Add("The MoveTo folder \"" + a.Argument + "\" does not exist. It will be created the first time the filter moves an event (check the spelling).");
						}
						break;
					case FilterActionType.Delete:
						warnings.Add("The filter has a Delete action. Deleted events cannot be recovered, and no later action or filter runs on a deleted event.");
						stopped = true;
						break;
					case FilterActionType.StopExecution:
						stopped = true;
						break;
					case FilterActionType.MarkRead:
						warnings.Add("MarkRead marks events as read for every user, not just you.");
						break;
					case FilterActionType.MarkUnread:
						warnings.Add("MarkUnread marks events as unread for every user, not just you.");
						break;
					case FilterActionType.SetColor:
						if (a.Argument == null || !rxHexColor.IsMatch(a.Argument.Trim()))
							warnings.Add("The SetColor argument \"" + a.Argument + "\" is not a valid hex color, so the action does nothing.");
						break;
				}
			}
			return warnings.Distinct().ToList();
		}
		private static string Truncate(string s, int max)
		{
			if (s == null || s.Length <= max)
				return s;
			return s.Substring(0, max) + "…";
		}
		/// <summary>
		/// Returns a summary of the enabled actions such as "MoveTo /Noise/Bots; StopExecution".
		/// </summary>
		public static string SummarizeActions(IEnumerable<FilterAction> actions)
		{
			return string.Join("; ", actions
				.Where(a => a.Enabled && a.Operator != FilterActionType.RememberFilterApplied)
				.OrderBy(a => a.FilterActionId)
				.Select(a => (a.Operator == FilterActionType.MoveTo || a.Operator == FilterActionType.SetColor) ? a.Operator + " " + a.Argument : a.Operator.ToString()));
		}
		#endregion

		#region Writes
		/// <summary>
		/// Creates a filter with its conditions and actions, in one transaction.  Returns the new filter's ID.
		/// </summary>
		/// <param name="db">Project database.</param>
		/// <param name="def">Definition (must be valid).</param>
		/// <param name="position">Where to put the filter in the execution order.</param>
		/// <param name="updatedBy">Who created the filter (see <see cref="Filter.UpdatedBy"/>).</param>
		/// <returns></returns>
		public static int Create(DB db, FilterDefinition def, FilterPosition position, string updatedBy)
		{
			List<string> problems = Validate(def);
			if (problems.Count > 0)
				throw new FilterValidationException(problems);
			int filterId = 0;
			db.RunInTransaction(() =>
			{
				string name = def.name.Trim();
				if (db.GetAllFiltersSummary().Any(s => s.filter.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
					throw new FilterValidationException(new List<string>() { "A filter named \"" + name + "\" already exists." });
				Filter f = new Filter()
				{
					Name = name,
					Enabled = def.enabled,
					ConditionHandling = def.conditionHandling,
					MyOrder = int.MaxValue, // Assigned below.
					UpdatedBy = updatedBy,
					UpdatedDate = TimeUtil.GetTimeInMsSinceEpoch()
				};
				db.InsertFilter(f);
				filterId = f.FilterId;
				foreach (ConditionDefinition c in def.conditions)
					db.InsertFilterCondition(ToCondition(c, filterId, 0));
				foreach (ActionDefinition a in def.actions)
					db.InsertFilterAction(ToAction(a, filterId, 0));
				ApplyPosition(db, f, position ?? new FilterPosition());
			});
			return filterId;
		}
		/// <summary>
		/// Replaces a filter's definition (and optionally its position), in one transaction.  Conditions and actions are added, changed, removed and reordered as needed.
		/// </summary>
		/// <param name="db">Project database.</param>
		/// <param name="filterId">ID of the filter.</param>
		/// <param name="etag">The etag the caller last saw.  If it doesn't match, <see cref="FilterConflictException"/> is thrown.</param>
		/// <param name="def">New definition (must be valid).</param>
		/// <param name="position">If not null, the new position in the execution order.</param>
		/// <param name="updatedBy">Who changed the filter (see <see cref="Filter.UpdatedBy"/>).</param>
		/// <returns>False if the filter does not exist.</returns>
		public static bool Update(DB db, int filterId, string etag, FilterDefinition def, FilterPosition position, string updatedBy)
		{
			List<string> problems = Validate(def);
			if (problems.Count > 0)
				throw new FilterValidationException(problems);
			bool found = false;
			db.RunInTransaction(() =>
			{
				// Lock the filter row so that concurrent updates are serialized and the etag check is reliable.
				db.FetchSql<int>(PetaPoco.Sql.Builder.Append("SELECT FilterId FROM " + db.ProjectNameLower + ".Filter WHERE FilterId = @0 FOR UPDATE", filterId));
				FullFilter current = db.GetFilter(filterId);
				if (current == null)
					return;
				found = true;
				if (!string.Equals(ComputeEtag(current), etag?.Trim(), StringComparison.OrdinalIgnoreCase))
					throw new FilterConflictException(current);
				string name = def.name.Trim();
				if (db.GetAllFiltersSummary().Any(s => s.filter.FilterId != filterId && s.filter.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
					throw new FilterValidationException(new List<string>() { "A filter named \"" + name + "\" already exists." });

				current.filter.Name = name;
				current.filter.Enabled = def.enabled;
				current.filter.ConditionHandling = def.conditionHandling;
				current.filter.UpdatedBy = updatedBy;
				current.filter.UpdatedDate = TimeUtil.GetTimeInMsSinceEpoch();
				db.UpdateFilterRow(current.filter);

				// Conditions and actions execute in ID order.  Writing the new list onto the existing rows in ID order,
				// inserting rows for any extra items (they get higher IDs) and deleting leftover rows produces exactly
				// the new order.
				FilterCondition[] oldConditions = current.conditions.OrderBy(c => c.FilterConditionId).ToArray();
				for (int i = 0; i < def.conditions.Count; i++)
				{
					if (i < oldConditions.Length)
						db.UpdateFilterCondition(ToCondition(def.conditions[i], filterId, oldConditions[i].FilterConditionId));
					else
						db.InsertFilterCondition(ToCondition(def.conditions[i], filterId, 0));
				}
				for (int i = def.conditions.Count; i < oldConditions.Length; i++)
					db.DeleteFilterCondition(oldConditions[i].FilterConditionId);

				FilterAction[] oldActions = current.actions.Where(a => a.Operator != FilterActionType.RememberFilterApplied).OrderBy(a => a.FilterActionId).ToArray();
				for (int i = 0; i < def.actions.Count; i++)
				{
					if (i < oldActions.Length)
						db.UpdateFilterAction(ToAction(def.actions[i], filterId, oldActions[i].FilterActionId));
					else
						db.InsertFilterAction(ToAction(def.actions[i], filterId, 0));
				}
				for (int i = def.actions.Count; i < oldActions.Length; i++)
					db.DeleteFilterAction(oldActions[i].FilterActionId);

				if (position != null)
					ApplyPosition(db, current.filter, position);
			});
			return found;
		}
		/// <summary>
		/// Deletes a filter if its etag matches.
		/// </summary>
		/// <returns>False if the filter does not exist.</returns>
		public static bool Delete(DB db, int filterId, string etag)
		{
			bool found = false;
			db.RunInTransaction(() =>
			{
				db.FetchSql<int>(PetaPoco.Sql.Builder.Append("SELECT FilterId FROM " + db.ProjectNameLower + ".Filter WHERE FilterId = @0 FOR UPDATE", filterId));
				FullFilter current = db.GetFilter(filterId);
				if (current == null)
					return;
				found = true;
				if (!string.Equals(ComputeEtag(current), etag?.Trim(), StringComparison.OrdinalIgnoreCase))
					throw new FilterConflictException(current);
				db.DeleteFilter(filterId);
			});
			return found;
		}
		/// <summary>
		/// Moves the filter to the requested position by changing MyOrder values.  Other filters' values are only changed when there is no room between their neighbors, so their etags rarely change.
		/// </summary>
		private static void ApplyPosition(DB db, Filter target, FilterPosition position)
		{
			List<Filter> others = db.GetAllFiltersSummary()
				.Select(s => s.filter)
				.Where(f => f.FilterId != target.FilterId)
				.OrderBy(f => f.MyOrder).ThenBy(f => f.FilterId)
				.ToList();
			int index;
			string kind = (position.Kind ?? "last").ToLower();
			if (kind == "first")
				index = 0;
			else if (kind == "last")
				index = others.Count;
			else if (kind == "before" || kind == "after")
			{
				int rel = others.FindIndex(f => f.FilterId == position.RelativeToFilterId);
				if (rel == -1)
				{
					if (position.RelativeToFilterId == target.FilterId)
						throw new FilterValidationException(new List<string>() { "position cannot refer to the filter itself." });
					throw new FilterValidationException(new List<string>() { "position refers to filter " + position.RelativeToFilterId + ", which does not exist." });
				}
				index = kind == "before" ? rel : rel + 1;
			}
			else
				throw new FilterValidationException(new List<string>() { "position must be \"first\", \"last\", {\"before\": filterId} or {\"after\": filterId}." });

			long? low = index > 0 ? others[index - 1].MyOrder : (long?)null;
			long? high = index < others.Count ? others[index].MyOrder : (long?)null;
			long? newOrder = null;
			if (low == null && high == null)
				newOrder = 1;
			else if (low == null)
				newOrder = high.Value - 1;
			else if (high == null)
				newOrder = low.Value + 1;
			else if (high.Value - low.Value >= 2)
				newOrder = low.Value + (high.Value - low.Value) / 2;

			// Keep the target where it is if it already satisfies the requested position.
			if (newOrder != null && (low == null || target.MyOrder > low.Value) && (high == null || target.MyOrder < high.Value) && target.MyOrder != int.MaxValue)
				return;

			if (newOrder != null && newOrder.Value > int.MinValue && newOrder.Value < int.MaxValue)
			{
				target.MyOrder = (int)newOrder.Value;
				db.ReorderFilters(new FilterOrder[] { new FilterOrder() { FilterId = target.FilterId, MyOrder = target.MyOrder } });
			}
			else
			{
				// No room: renumber everything with gaps.
				others.Insert(index, target);
				List<FilterOrder> newOrders = new List<FilterOrder>();
				for (int i = 0; i < others.Count; i++)
				{
					int order = (i + 1) * 10;
					if (others[i].MyOrder != order || others[i] == target)
						newOrders.Add(new FilterOrder() { FilterId = others[i].FilterId, MyOrder = order });
					others[i].MyOrder = order;
				}
				db.ReorderFilters(newOrders.ToArray());
			}
		}
		#endregion
	}
}

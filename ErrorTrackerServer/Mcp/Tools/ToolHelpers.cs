using ErrorTrackerServer.Database.Project.Model;
using ErrorTrackerServer.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ErrorTrackerServer.Mcp.Tools
{
	/// <summary>
	/// Parses the event selection criteria shared by search_events and event_stats.
	/// </summary>
	public static class McpCriteria
	{
		/// <summary>
		/// Names of the criteria arguments, used to fingerprint queries for cursors.
		/// </summary>
		public static readonly string[] ArgumentNames = new string[] { "project", "text", "contains", "conditions", "match", "eventType", "subType", "hashValue", "folder", "includeSubfolders", "since", "until" };

		/// <summary>
		/// Builds <see cref="EventCriteria"/> from the tool arguments.
		/// </summary>
		/// <param name="ctx">Call context.</param>
		/// <param name="args">Tool arguments.</param>
		/// <param name="folders">The project's folder structure.</param>
		/// <param name="defaultSince">Value used when "since" is omitted, e.g. "30d" or "all".</param>
		/// <param name="appliedSince">(Output) The lower time bound actually used, as ISO 8601, or "all".</param>
		/// <returns></returns>
		public static EventCriteria Parse(McpCallContext ctx, JObject args, FolderStructure folders, string defaultSince, out string appliedSince)
		{
			EventCriteria c = new EventCriteria();
			ParseTimeRange(args, defaultSince, c, out appliedSince);

			if (args["folder"] != null && args["folder"].Type != JTokenType.Null)
			{
				FolderStructure folder = McpArgs.ResolveFolder(folders, args["folder"]);
				c.FolderIds = McpArgs.GetFolderIds(folder, McpArgs.GetBool(args, "includeSubfolders", false));
			}
			else if (McpArgs.GetBool(args, "includeSubfolders", false))
				throw new McpToolException("\"includeSubfolders\" only has an effect together with \"folder\". Omit both to search all folders.");

			c.Text = McpArgs.GetString(args, "text");
			c.Contains = McpArgs.GetString(args, "contains");
			c.SubType = McpArgs.GetString(args, "subType");
			c.HashValue = McpArgs.GetString(args, "hashValue");
			string[] eventTypes = McpArgs.GetStringArray(args, "eventType");
			if (eventTypes != null)
				c.EventTypes = eventTypes.Select(t => (EventType)Enum.Parse(typeof(EventType), t)).ToArray();
			c.MatchAll = McpArgs.GetString(args, "match", "all") != "any";
			c.Conditions = McpArgs.ParseConditions(args["conditions"]);
			if (c.Conditions != null)
			{
				foreach (FilterCondition condition in c.Conditions.Where(x => x.Enabled))
				{
					string key = condition.TagKey.Trim();
					if (key.Equals("Date", StringComparison.OrdinalIgnoreCase))
						throw new McpToolException("\"Date\" conditions are not supported in searches. Use \"since\" and \"until\" instead (ISO 8601 or durations like \"24h\").");
					if (key.Equals("Folder", StringComparison.OrdinalIgnoreCase))
						throw new McpToolException("\"Folder\" conditions are not supported in searches. Use the \"folder\" argument (with \"includeSubfolders\") instead.");
					ctx.CheckNotHiddenTag(key, "A condition");
				}
			}
			c.ExcludedTagKeys = ctx.Redactor.HiddenTagKeys.ToArray();
			return c;
		}
		/// <summary>
		/// Parses "since" and "until" into the criteria.
		/// </summary>
		public static void ParseTimeRange(JObject args, string defaultSince, EventCriteria c, out string appliedSince)
		{
			string since = McpArgs.GetString(args, "since") ?? defaultSince;
			if (since == null || since.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
			{
				c.Since = null;
				appliedSince = "all";
			}
			else
			{
				c.Since = McpOutput.ParseTime(since, "since");
				appliedSince = McpOutput.Iso(c.Since.Value);
			}
			string until = McpArgs.GetString(args, "until");
			if (until != null)
				c.Until = McpOutput.ParseTime(until, "until");
			if (c.Since != null && c.Until != null && c.Since.Value >= c.Until.Value)
				throw new McpToolException("\"since\" (" + McpOutput.Iso(c.Since.Value) + ") must be earlier than \"until\" (" + McpOutput.Iso(c.Until.Value) + ").");
		}
		/// <summary>
		/// Runs a query with the configured statement timeout and turns database errors the agent can fix into tool errors.
		/// </summary>
		public static void RunQuery(McpCallContext ctx, DB db, Action action)
		{
			try
			{
				db.RunWithStatementTimeout(ctx.QueryTimeoutSeconds, action);
			}
			catch (QueryCriteriaException ex)
			{
				throw new McpToolException(ex.Message);
			}
			catch (Exception ex) when (DB.IsStatementTimeout(ex))
			{
				throw new McpToolException("The query timed out after " + ctx.QueryTimeoutSeconds + " s. Narrow it: use a more recent \"since\", a \"folder\", \"eventType\", \"hashValue\", or \"text\" instead of \"contains\".");
			}
			catch (Exception ex) when (DB.GetPostgresSqlState(ex) == "2201B")
			{
				throw new McpToolException("A regular expression in the conditions is not valid in PostgreSQL's regex dialect (searches use PostgreSQL's ~* operator, not .NET): " + GetPostgresMessage(ex));
			}
			catch (Exception ex) when (DB.GetPostgresSqlState(ex) != null && DB.GetPostgresSqlState(ex).StartsWith("22"))
			{
				throw new McpToolException("The database rejected a value in the query: " + GetPostgresMessage(ex));
			}
		}
		private static string GetPostgresMessage(Exception ex)
		{
			for (Exception e = ex; e != null; e = e.InnerException)
				if (e is Npgsql.PostgresException pe)
					return pe.MessageText;
			return ex.Message;
		}
	}

	/// <summary>
	/// Opaque paging cursors.  A cursor carries a keyset position and a fingerprint of the query it belongs to.
	/// </summary>
	public static class McpCursor
	{
		/// <summary>
		/// Returns a fingerprint of the named arguments.
		/// </summary>
		public static string Fingerprint(JObject args, IEnumerable<string> names)
		{
			JObject subset = new JObject();
			foreach (string name in names)
				if (args[name] != null)
					subset[name] = args[name];
			using (SHA256 sha = SHA256.Create())
				return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(subset.ToString(Formatting.None)))).Substring(0, 12);
		}
		public static string Encode(JObject position, string fingerprint)
		{
			position["f"] = fingerprint;
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(position.ToString(Formatting.None))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		}
		/// <summary>
		/// Decodes a cursor, or throws a tool error if it is malformed or belongs to a different query.
		/// </summary>
		public static JObject Decode(string cursor, string fingerprint)
		{
			JObject o;
			try
			{
				string b64 = cursor.Trim().Replace('-', '+').Replace('_', '/');
				b64 += new string('=', (4 - b64.Length % 4) % 4);
				o = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b64)));
			}
			catch (Exception)
			{
				throw new McpToolException("\"cursor\" is not a valid cursor. Pass the nextCursor value from the previous result unchanged, or omit cursor to start from the beginning.");
			}
			if (o.Value<string>("f") != fingerprint)
				throw new McpToolException("\"cursor\" belongs to a query with different arguments. Repeat the exact same arguments with the cursor, or omit cursor to start a new query.");
			return o;
		}
	}

	/// <summary>
	/// Helpers for building filter output.
	/// </summary>
	public static class McpFilterOutput
	{
		/// <summary>
		/// Converts a definition to the JSON FilterDefinition shape.
		/// </summary>
		public static JObject Definition(FilterDefinition def)
		{
			return new JObject()
			{
				["name"] = def.name,
				["enabled"] = def.enabled,
				["conditionHandling"] = def.conditionHandling.ToString(),
				["conditions"] = new JArray(def.conditions.Select(c => new JObject()
				{
					["tagKey"] = c.tagKey,
					["operator"] = c.op.ToString(),
					["query"] = c.query,
					["regex"] = c.regex,
					["invert"] = c.invert,
					["enabled"] = c.enabled
				})),
				["actions"] = new JArray(def.actions.Select(a =>
				{
					JObject o = new JObject() { ["type"] = a.type.ToString() };
					if (!string.IsNullOrEmpty(a.argument))
						o["argument"] = a.argument;
					o["enabled"] = a.enabled;
					return o;
				}))
			};
		}
		/// <summary>
		/// Converts a stored filter to the get_filter output shape.
		/// </summary>
		public static JObject Filter(string projectName, FullFilter full, FilterAppliedStats stats)
		{
			JObject o = Definition(FilterService.ToDefinition(full));
			o.AddFirst(new JProperty("filterId", full.filter.FilterId));
			o["order"] = full.filter.MyOrder;
			o["etag"] = FilterService.ComputeEtag(full);
			o["timesApplied"] = stats?.TimesApplied ?? 0;
			if (stats != null)
				o["lastApplied"] = McpOutput.Iso(stats.LastApplied);
			if (!string.IsNullOrEmpty(full.filter.UpdatedBy))
				o["updatedBy"] = full.filter.UpdatedBy;
			if (full.filter.UpdatedDate != null)
				o["updatedDate"] = McpOutput.Iso(full.filter.UpdatedDate.Value);
			string url = McpOutput.FilterUrl(projectName, full.filter.FilterId);
			if (url != null)
				o["webUrl"] = url;
			return o;
		}
		/// <summary>
		/// Loads a filter with its stats, or returns null if it does not exist.
		/// </summary>
		public static JObject LoadFilter(DB db, string projectName, int filterId)
		{
			FullFilter full = db.GetFilter(filterId);
			if (full == null)
				return null;
			db.GetFilterAppliedStats().TryGetValue(filterId, out FilterAppliedStats stats);
			return Filter(projectName, full, stats);
		}
		/// <summary>
		/// Throws a tool error if any enabled condition refers to a hidden tag (spec RED-4).
		/// </summary>
		public static void CheckHiddenTags(McpCallContext ctx, FullFilter full)
		{
			if (full.filter.ConditionHandling == ConditionHandling.Unconditional)
				return;
			foreach (FilterCondition c in full.conditions.Where(c => c.Enabled))
				ctx.CheckNotHiddenTag(c.TagKey, "A filter condition");
		}

		/// <summary>
		/// Output schema of a stored filter.
		/// </summary>
		public static JObject Schema()
		{
			return new JObject()
			{
				["type"] = "object",
				["properties"] = new JObject()
				{
					["filterId"] = new JObject() { ["type"] = "integer" },
					["name"] = new JObject() { ["type"] = "string" },
					["enabled"] = new JObject() { ["type"] = "boolean" },
					["conditionHandling"] = new JObject() { ["type"] = "string" },
					["conditions"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "object" } },
					["actions"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "object" } },
					["order"] = new JObject() { ["type"] = "integer" },
					["etag"] = new JObject() { ["type"] = "string" },
					["timesApplied"] = new JObject() { ["type"] = "integer" },
					["lastApplied"] = new JObject() { ["type"] = "string" },
					["updatedBy"] = new JObject() { ["type"] = "string" },
					["updatedDate"] = new JObject() { ["type"] = "string" },
					["webUrl"] = new JObject() { ["type"] = "string" }
				},
				["required"] = new JArray("filterId", "name", "enabled", "conditionHandling", "conditions", "actions", "order", "etag", "timesApplied")
			};
		}
	}

	/// <summary>
	/// Output schema fragments.
	/// </summary>
	public static class McpOutputSchemas
	{
		public static JObject T(string type)
		{
			return new JObject() { ["type"] = type };
		}
		public static JObject Arr(JObject items)
		{
			return new JObject() { ["type"] = "array", ["items"] = items };
		}
		public static JObject Obj(JObject properties, params string[] required)
		{
			JObject o = new JObject() { ["type"] = "object", ["properties"] = properties };
			if (required.Length > 0)
				o["required"] = new JArray(required);
			return o;
		}
		public static JObject EventSummary()
		{
			return Obj(new JObject()
			{
				["eventId"] = T("integer"),
				["date"] = T("string"),
				["eventType"] = T("string"),
				["subType"] = T("string"),
				["messagePreview"] = T("string"),
				["messageWasHtml"] = T("boolean"),
				["folderId"] = T("integer"),
				["folderPath"] = T("string"),
				["color"] = T("string"),
				["hashValue"] = T("string"),
				["read"] = T("boolean"),
				["tags"] = new JObject() { ["type"] = "object", ["additionalProperties"] = T("string") },
				["webUrl"] = T("string")
			}, "eventId", "date", "eventType", "subType", "messagePreview", "messageWasHtml", "folderId", "folderPath", "color", "hashValue", "read");
		}
	}
}

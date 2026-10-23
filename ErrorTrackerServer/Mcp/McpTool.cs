using ErrorTrackerServer.Database.Global.Model;
using ErrorTrackerServer.Database.Project.Model;
using ErrorTrackerServer.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ErrorTrackerServer.Mcp
{
	/// <summary>
	/// An error during tool execution (project not found, permission denied, timeout, conflict, ...).  Reported to the client as a normal tool result with isError: true, so the message should tell the agent how to recover.
	/// </summary>
	public class McpToolException : Exception
	{
		public McpToolException(string message) : base(message) { }
	}
	/// <summary>
	/// Invalid tool arguments which JSON Schema validation could not catch.  Reported, like schema violations, as a tool result with isError: true so that the model can correct its arguments.
	/// </summary>
	public class McpInvalidParamsException : Exception
	{
		public McpInvalidParamsException(string message) : base(message) { }
	}

	/// <summary>
	/// The context of one tools/call request.
	/// </summary>
	public class McpCallContext
	{
		public ApiToken Token;
		public User User;
		public string IPAddress;
		public McpRedactor Redactor;
		/// <summary>
		/// The project named by the call's "project" argument, once validated.  Recorded in the audit log.
		/// </summary>
		public string ProjectName;

		public bool HasScope(string scope)
		{
			return Token.HasScope(scope);
		}
		/// <summary>
		/// How the token's actions are attributed in logs and on filters, e.g. "admin (via token \"Claude\")".
		/// </summary>
		public string Actor
		{
			get { return User.Name + " (via token \"" + Token.Name + "\")"; }
		}
		/// <summary>
		/// Statement timeout for queries, in seconds.
		/// </summary>
		public int QueryTimeoutSeconds
		{
			get { return Math.Max(1, Math.Min(3600, Settings.data.mcpQueryTimeoutSeconds)); }
		}
		/// <summary>
		/// Returns true if the token may read the project (spec AUTH-6: the user's current access, intersected with the token's project allow-list).
		/// </summary>
		public bool CanRead(Project p)
		{
			return Token.AllowsProject(p.Name) && (User.IsProjectAllowed(p.Name) || User.IsProjectAllowedReadOnly(p.Name));
		}
		/// <summary>
		/// Returns true if the token may write to the project.
		/// </summary>
		public bool CanWrite(Project p)
		{
			return Token.AllowsProject(p.Name) && User.IsProjectAllowed(p.Name);
		}
		/// <summary>
		/// Validates the "project" argument and access to it.
		/// </summary>
		/// <param name="args">Tool arguments.</param>
		/// <param name="needsWriteAccess">True if the tool needs write access.</param>
		/// <returns></returns>
		public Project RequireProject(JObject args, bool needsWriteAccess)
		{
			return RequireProject(McpArgs.GetString(args, "project"), needsWriteAccess);
		}
		/// <summary>
		/// Validates a project name and access to it.
		/// </summary>
		public Project RequireProject(string projectName, bool needsWriteAccess)
		{
			Project p = string.IsNullOrWhiteSpace(projectName) ? null : Settings.data.GetProject(projectName.Trim());
			if (p == null || !CanRead(p))
				throw new McpToolException("Project \"" + McpOutput.Truncate(projectName, 100) + "\" was not found, or this token cannot access it. Call list_projects to see the projects available to this token.");
			if (needsWriteAccess && !CanWrite(p))
				throw new McpToolException("This token has read-only access to project \"" + p.Name + "\". Writing filters or running them requires write access to the project, which must be granted to user \"" + User.Name + "\" by an administrator.");
			ProjectName = p.Name;
			return p;
		}
		/// <summary>
		/// Throws a tool error if the condition refers to a hidden tag (spec RED-4).
		/// </summary>
		public void CheckNotHiddenTag(string tagKey, string usage)
		{
			if (Redactor.IsHiddenTag(tagKey))
				throw new McpToolException(usage + " refers to tag \"" + tagKey + "\", which the server's redaction policy hides from this token. Remove it and try again.");
		}
	}

	/// <summary>
	/// Base class for MCP tools.
	/// </summary>
	public abstract class McpTool
	{
		/// <summary>
		/// Tool name, e.g. "search_events".
		/// </summary>
		public abstract string Name { get; }
		/// <summary>
		/// Human-readable title.
		/// </summary>
		public abstract string Title { get; }
		/// <summary>
		/// Description for the agent.
		/// </summary>
		public abstract string Description { get; }
		/// <summary>
		/// The token scope required to see and call the tool.
		/// </summary>
		public abstract string RequiredScope { get; }
		public virtual bool ReadOnlyHint { get { return true; } }
		public virtual bool DestructiveHint { get { return false; } }
		public virtual bool IdempotentHint { get { return true; } }
		/// <summary>
		/// JSON Schema of the arguments.
		/// </summary>
		public abstract JObject InputSchema { get; }
		/// <summary>
		/// JSON Schema of the result, or null.
		/// </summary>
		public virtual JObject OutputSchema { get { return null; } }
		/// <summary>
		/// Executes the tool.  Throw <see cref="McpToolException"/> for errors the agent can recover from.
		/// </summary>
		/// <param name="ctx">Call context.</param>
		/// <param name="args">Arguments, already validated against <see cref="InputSchema"/>.</param>
		/// <returns>The result object.</returns>
		public abstract JObject Execute(McpCallContext ctx, JObject args);

		/// <summary>
		/// Returns the tool definition for tools/list.
		/// </summary>
		public JObject GetDefinition()
		{
			JObject def = new JObject();
			def["name"] = Name;
			def["title"] = Title;
			def["description"] = Description;
			def["inputSchema"] = InputSchema;
			JObject output = OutputSchema;
			if (output != null)
				def["outputSchema"] = output;
			def["annotations"] = new JObject()
			{
				["title"] = Title,
				["readOnlyHint"] = ReadOnlyHint,
				["destructiveHint"] = DestructiveHint,
				["idempotentHint"] = IdempotentHint,
				["openWorldHint"] = false
			};
			return def;
		}
	}

	/// <summary>
	/// Helpers for reading tool arguments.  Arguments have already been validated against the tool's schema, so these only handle defaults and conversions.
	/// </summary>
	public static class McpArgs
	{
		public static string GetString(JObject args, string name, string defaultValue = null)
		{
			JToken t = args[name];
			if (t == null || t.Type == JTokenType.Null)
				return defaultValue;
			return t.Type == JTokenType.String ? t.Value<string>() : t.ToString(Newtonsoft.Json.Formatting.None);
		}
		public static bool GetBool(JObject args, string name, bool defaultValue)
		{
			JToken t = args[name];
			if (t == null || t.Type == JTokenType.Null)
				return defaultValue;
			return t.Value<bool>();
		}
		public static int GetInt(JObject args, string name, int defaultValue)
		{
			JToken t = args[name];
			if (t == null || t.Type == JTokenType.Null)
				return defaultValue;
			return (int)Math.Round(t.Value<double>());
		}
		public static int? GetNullableInt(JObject args, string name)
		{
			JToken t = args[name];
			if (t == null || t.Type == JTokenType.Null)
				return null;
			return (int)Math.Round(t.Value<double>());
		}
		public static long[] GetLongArray(JObject args, string name)
		{
			if (!(args[name] is JArray arr))
				return null;
			return arr.Select(t => (long)Math.Round(t.Value<double>())).ToArray();
		}
		public static string[] GetStringArray(JObject args, string name)
		{
			if (!(args[name] is JArray arr))
				return null;
			return arr.Select(t => t.Value<string>()).ToArray();
		}
		/// <summary>
		/// Parses an array of Condition objects.
		/// </summary>
		public static FilterCondition[] ParseConditions(JToken token)
		{
			if (!(token is JArray arr))
				return null;
			return arr.OfType<JObject>().Select(c => new FilterCondition()
			{
				TagKey = c.Value<string>("tagKey")?.Trim() ?? "",
				Operator = (FilterConditionOperator)Enum.Parse(typeof(FilterConditionOperator), c.Value<string>("operator")),
				Query = c.Value<string>("query") ?? "",
				Regex = c["regex"]?.Value<bool>() ?? false,
				Invert = c["invert"]?.Value<bool>() ?? false,
				Enabled = c["enabled"]?.Value<bool>() ?? true
			}).ToArray();
		}
		/// <summary>
		/// Parses a FilterDefinition object.
		/// </summary>
		public static FilterDefinition ParseDefinition(JToken token)
		{
			if (!(token is JObject d))
				return null;
			FilterDefinition def = new FilterDefinition();
			def.name = d.Value<string>("name");
			def.enabled = d.Value<bool>("enabled");
			def.conditionHandling = (ConditionHandling)Enum.Parse(typeof(ConditionHandling), d.Value<string>("conditionHandling"));
			def.conditions = (ParseConditions(d["conditions"]) ?? new FilterCondition[0]).Select(c => new ConditionDefinition()
			{
				tagKey = c.TagKey,
				op = c.Operator,
				query = c.Query,
				regex = c.Regex,
				invert = c.Invert,
				enabled = c.Enabled
			}).ToList();
			def.actions = ((d["actions"] as JArray) ?? new JArray()).OfType<JObject>().Select(a => new ActionDefinition()
			{
				type = (FilterActionType)Enum.Parse(typeof(FilterActionType), a.Value<string>("type")),
				argument = a.Value<string>("argument") ?? "",
				enabled = a["enabled"]?.Value<bool>() ?? true
			}).ToList();
			return def;
		}
		/// <summary>
		/// Parses a position argument: "first", "last", {"before": id} or {"after": id}.  Returns null if absent.
		/// </summary>
		public static FilterPosition ParsePosition(JToken token)
		{
			if (token == null || token.Type == JTokenType.Null)
				return null;
			if (token.Type == JTokenType.String)
				return new FilterPosition() { Kind = token.Value<string>() };
			JObject o = (JObject)token;
			if (o["before"] != null)
				return new FilterPosition() { Kind = "before", RelativeToFilterId = o.Value<int>("before") };
			return new FilterPosition() { Kind = "after", RelativeToFilterId = o.Value<int>("after") };
		}
		/// <summary>
		/// Resolves a "folder" argument (folder ID or absolute path) to a folder.  Never creates folders.
		/// </summary>
		public static FolderStructure ResolveFolder(FolderStructure root, JToken folderArg)
		{
			FolderStructure node = null;
			if (folderArg.Type == JTokenType.Integer || folderArg.Type == JTokenType.Float)
				root.TryGetNode((int)Math.Round(folderArg.Value<double>()), out node);
			else
			{
				string path = folderArg.Value<string>()?.Trim();
				try
				{
					node = root.ResolvePath(path, null);
				}
				catch (Exception)
				{
					node = null;
				}
			}
			if (node == null)
				throw new McpToolException("Folder " + folderArg.ToString(Newtonsoft.Json.Formatting.None) + " was not found. Call list_folders to see folder IDs and paths (paths look like \"/Parent/Child\"; the root folder is ID 0, path \"/\").");
			return node;
		}
		/// <summary>
		/// Returns the IDs of the folder and, optionally, all folders below it.
		/// </summary>
		public static int[] GetFolderIds(FolderStructure folder, bool includeSubfolders)
		{
			List<int> ids = new List<int>();
			void Add(FolderStructure f)
			{
				ids.Add(f.FolderId);
				if (includeSubfolders)
					foreach (FolderStructure child in f.Children)
						Add(child);
			}
			Add(folder);
			return ids.ToArray();
		}
	}

	/// <summary>
	/// JSON Schema fragments shared by several tools.
	/// </summary>
	public static class McpSchemas
	{
		public static JObject Object(JObject properties, params string[] required)
		{
			JObject o = new JObject()
			{
				["type"] = "object",
				["properties"] = properties,
				["additionalProperties"] = false
			};
			if (required.Length > 0)
				o["required"] = new JArray(required);
			return o;
		}
		public static JObject Str(string description, int? maxLength = null)
		{
			JObject o = new JObject() { ["type"] = "string", ["description"] = description };
			if (maxLength != null)
				o["maxLength"] = maxLength.Value;
			return o;
		}
		public static JObject Bool(string description)
		{
			return new JObject() { ["type"] = "boolean", ["description"] = description };
		}
		public static JObject Int(string description, long? min = null, long? max = null)
		{
			JObject o = new JObject() { ["type"] = "integer", ["description"] = description };
			if (min != null)
				o["minimum"] = min.Value;
			if (max != null)
				o["maximum"] = max.Value;
			return o;
		}
		public static JObject Enum(string description, params string[] values)
		{
			return new JObject() { ["type"] = "string", ["enum"] = new JArray(values), ["description"] = description };
		}
		public static JObject Array(JObject items, string description, int? minItems = null, int? maxItems = null)
		{
			JObject o = new JObject() { ["type"] = "array", ["items"] = items, ["description"] = description };
			if (minItems != null)
				o["minItems"] = minItems.Value;
			if (maxItems != null)
				o["maxItems"] = maxItems.Value;
			return o;
		}
		public static JObject Project()
		{
			return Str("Project name (case-insensitive). See list_projects.", 200);
		}
		public static JObject Folder(string description)
		{
			return new JObject()
			{
				["description"] = description + " A folder ID (number) or an absolute path (string) like \"/Parent/Child\". The root folder is ID 0, path \"/\".",
				["anyOf"] = new JArray(
					new JObject() { ["type"] = "integer", ["minimum"] = 0 },
					new JObject() { ["type"] = "string", ["maxLength"] = 2000 })
			};
		}
		public static JObject Time(string description)
		{
			return Str(description + " An ISO 8601 timestamp (UTC unless an offset is given), or a duration back from now: \"30m\", \"24h\", \"7d\", \"4w\".", 100);
		}

		public const string ConditionSemantics =
			"Condition semantics (same as the filter engine): matching is case-insensitive. tagKey is a tag name (case-insensitive) or one of the reserved names \"EventType\", \"SubType\", \"Message\", \"Date\", \"Folder\", \"Color\". "
			+ "A missing tag counts as an empty string, so an inverted (invert: true) Contains condition MATCHES events that lack the tag. An empty tagKey never matches. "
			+ "Without regex, Contains/Equals/StartsWith/EndsWith are literal string tests. With regex: true, query is a regular expression and Equals/StartsWith/EndsWith add ^ and $ anchors automatically. "
			+ "\"Message\" sees the full raw stored message: HTML source when the event's message is HTML (see messageSearchPrefix in get_events). "
			+ "\"Color\" is the 6-digit hex color without \"#\" (e.g. \"EBEBEB\"). \"EventType\" is \"Error\", \"Info\" or \"Debug\".";

		/// <summary>
		/// Schema of a Condition.
		/// </summary>
		/// <param name="forFilters">True for filter definitions (.NET regex, Date/Folder allowed); false for searches (PostgreSQL regex, Date/Folder rejected).</param>
		public static JObject Condition(bool forFilters)
		{
			string description = ConditionSemantics + " "
				+ (forFilters
					? "In filters, regexes are .NET regular expressions evaluated with Singleline and IgnoreCase options (250 ms timeout). \"Folder\" compares against the event's absolute folder path at that moment in the run (an earlier filter's MoveTo changes it). \"Date\" compares against a text rendering like \"2026/10/23 02:05:09 PM\" (UTC, 12-hour clock), which is rarely useful."
					: "In searches, regexes use PostgreSQL's regex dialect (~*), not .NET's. \"Date\" and \"Folder\" conditions are rejected: use since/until and folder instead.");
			return new JObject()
			{
				["type"] = "object",
				["description"] = description,
				["properties"] = new JObject()
				{
					["tagKey"] = Str("Tag name, or a reserved name: EventType, SubType, Message, Date, Folder, Color.", 128),
					["operator"] = Enum("String test to apply.", "Contains", "Equals", "StartsWith", "EndsWith"),
					["query"] = Str("Text or regular expression to test for.", 10000),
					["regex"] = Bool("If true, query is a regular expression. Default false."),
					["invert"] = Bool("If true, the condition matches when the test fails (e.g. \"does not contain\"). Default false."),
					["enabled"] = Bool("If false, the condition is ignored. Default true.")
				},
				["required"] = new JArray("tagKey", "operator", "query"),
				["additionalProperties"] = false
			};
		}
		/// <summary>
		/// Schema of an Action.
		/// </summary>
		public static JObject Action()
		{
			return new JObject()
			{
				["type"] = "object",
				["description"] = "A filter action. Actions run in order. Delete and StopExecution end processing of the event: no later action or filter runs on it. "
					+ "MarkRead and MarkUnread change the read state for EVERY user, not just you. MoveTo creates missing folders automatically, so a typo in the path silently creates a new folder.",
				["properties"] = new JObject()
				{
					["type"] = Enum("Action type.", "MoveTo", "Delete", "SetColor", "StopExecution", "MarkRead", "MarkUnread"),
					["argument"] = Str("MoveTo: absolute folder path such as \"/Noise/Bots\". SetColor: hex color \"#RRGGBB\". Ignored by the other types.", 2000),
					["enabled"] = Bool("If false, the action is skipped. Default true.")
				},
				["required"] = new JArray("type"),
				["additionalProperties"] = false
			};
		}
		/// <summary>
		/// Schema of a FilterDefinition.
		/// </summary>
		public static JObject FilterDefinition()
		{
			return new JObject()
			{
				["type"] = "object",
				["description"] = "A complete filter. Filters run in ascending order on every new event (and when run_filters is used). conditionHandling \"All\" requires every enabled condition (and never matches if none is enabled); \"Any\" requires one; \"Unconditional\" matches every event and ignores conditions.",
				["properties"] = new JObject()
				{
					["name"] = Str("Unique filter name (ASCII printable characters).", 200),
					["enabled"] = Bool("Required. If true, the filter runs on every new event. Recommended: create filters disabled, check them with preview_filter, then enable them once the user agrees."),
					["conditionHandling"] = Enum("How conditions combine.", "All", "Any", "Unconditional"),
					["conditions"] = Array(Condition(true), "Conditions, in order.", null, 100),
					["actions"] = Array(Action(), "Actions, in execution order (the order matters).", null, 100)
				},
				["required"] = new JArray("name", "enabled", "conditionHandling", "conditions", "actions"),
				["additionalProperties"] = false
			};
		}
		/// <summary>
		/// Schema of a filter position argument.
		/// </summary>
		public static JObject Position()
		{
			return new JObject()
			{
				["description"] = "Where to put the filter in the execution order: \"first\", \"last\", {\"before\": filterId} or {\"after\": filterId}.",
				["anyOf"] = new JArray(
					new JObject() { ["type"] = "string", ["enum"] = new JArray("first", "last") },
					new JObject() { ["type"] = "object", ["properties"] = new JObject() { ["before"] = new JObject() { ["type"] = "integer" } }, ["required"] = new JArray("before"), ["additionalProperties"] = false },
					new JObject() { ["type"] = "object", ["properties"] = new JObject() { ["after"] = new JObject() { ["type"] = "integer" } }, ["required"] = new JArray("after"), ["additionalProperties"] = false })
			};
		}
		/// <summary>
		/// Adds the event selection criteria shared by search_events and event_stats to the properties object.
		/// </summary>
		public static void AddCriteriaProperties(JObject properties)
		{
			properties["text"] = Str("PostgreSQL full-text search in web-search syntax (\"quoted phrase\", -exclude, or). Matches whole (stemmed) words, not substrings: \"NullRef\" does NOT find \"NullReferenceException\"; use contains for that.", 1000);
			properties["contains"] = Str("Literal, case-insensitive substring searched in Message, SubType, EventType and every tag value. Slower than text.", 1000);
			properties["conditions"] = Array(Condition(false), "Conditions evaluated in SQL; combined according to match.", null, 50);
			properties["match"] = Enum("How conditions combine with each other: \"all\" (AND, default) or \"any\" (OR). Other criteria are always ANDed.", "all", "any");
			properties["eventType"] = Array(Enum("Event type.", "Error", "Info", "Debug"), "Only these event types.", null, 3);
			properties["subType"] = Str("Only events with exactly this SubType (case-insensitive).", 1000);
			properties["hashValue"] = Str("Only events with this hashValue: all occurrences of one error.", 100);
			properties["folder"] = Folder("Only events in this folder. Default: all folders.");
			properties["includeSubfolders"] = Bool("With folder: also include events in all folders below it. Default false.");
			properties["since"] = Time("Only events at or after this time (inclusive). Default \"30d\". Pass \"all\" for no lower bound.");
			properties["until"] = Time("Only events before this time (exclusive). Default: no upper bound.");
		}
	}
}

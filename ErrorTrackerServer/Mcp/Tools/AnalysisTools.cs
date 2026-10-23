using ErrorTrackerServer.Database.Global.Model;
using ErrorTrackerServer.Database.Project.Model;
using ErrorTrackerServer.Filtering;
using ErrorTrackerServer.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using static ErrorTrackerServer.Mcp.Tools.McpOutputSchemas;

namespace ErrorTrackerServer.Mcp.Tools
{
	public class EventStatsTool : McpTool
	{
		public override string Name { get { return "event_stats"; } }
		public override string Title { get { return "Event statistics"; } }
		public override string Description
		{
			get
			{
				return "Server-side aggregation of matching events, so you can answer \"what's new\" and \"is it getting worse\" without paging through events. "
					+ "Takes the same criteria as search_events (default range: last 30 days), groups the matching events, and returns the largest groups (by count) with first/last occurrence within the range and a sample event. "
					+ "groupBy \"hash\" groups by distinct error. onlyNewSince keeps only groups whose first-ever occurrence (any date, whole project) is at or after that time: use it with groupBy \"hash\" to find errors that first appeared after a deploy. "
					+ "bucket \"hour\" or \"day\" adds per-group counts per UTC hour/day (empty buckets omitted).";
			}
		}
		public override string RequiredScope { get { return ApiTokenScopes.EventsRead; } }
		public override JObject InputSchema
		{
			get
			{
				JObject props = new JObject() { ["project"] = McpSchemas.Project() };
				McpSchemas.AddCriteriaProperties(props);
				props["groupBy"] = new JObject()
				{
					["description"] = "What to group by: \"hash\" (distinct error), \"subType\", \"eventType\", \"folder\", or {\"tag\": \"Tag Name\"} (events without the tag form the group with key \"\").",
					["anyOf"] = new JArray(
						new JObject() { ["type"] = "string", ["enum"] = new JArray("hash", "subType", "eventType", "folder") },
						new JObject() { ["type"] = "object", ["properties"] = new JObject() { ["tag"] = McpSchemas.Str("Tag key.", 128) }, ["required"] = new JArray("tag"), ["additionalProperties"] = false })
				};
				props["bucket"] = McpSchemas.Enum("Per-group time series granularity (UTC boundaries). Default \"none\".", "none", "hour", "day");
				props["onlyNewSince"] = McpSchemas.Time("Only groups whose first-ever occurrence in the project is at or after this time. When given without since, since defaults to this value.");
				props["limit"] = McpSchemas.Int("Maximum number of groups (largest first). Default 25.", 1, 200);
				return McpSchemas.Object(props, "project", "groupBy");
			}
		}
		public override JObject OutputSchema
		{
			get
			{
				return Obj(new JObject()
				{
					["groups"] = Arr(Obj(new JObject()
					{
						["key"] = T("string"),
						["count"] = T("integer"),
						["firstSeen"] = T("string"),
						["lastSeen"] = T("string"),
						["firstSeenEver"] = T("string"),
						["sampleEventId"] = T("integer"),
						["sampleMessagePreview"] = T("string"),
						["buckets"] = Arr(Obj(new JObject() { ["start"] = T("string"), ["count"] = T("integer") }, "start", "count"))
					}, "key", "count", "firstSeen", "lastSeen", "sampleEventId", "sampleMessagePreview")),
					["totalMatching"] = T("integer"),
					["totalGroups"] = T("integer"),
					["truncated"] = T("boolean"),
					["appliedSince"] = T("string"),
					["serverTimeUtc"] = T("string")
				}, "groups", "totalMatching", "appliedSince", "serverTimeUtc");
			}
		}
		public override JObject Execute(McpCallContext ctx, JObject args)
		{
			Project p = ctx.RequireProject(args, false);
			string serverTime = McpOutput.NowIso();
			StatsGroupBy groupBy;
			string tagKey = null;
			JToken gb = args["groupBy"];
			if (gb.Type == JTokenType.Object)
			{
				groupBy = StatsGroupBy.Tag;
				tagKey = gb.Value<string>("tag")?.Trim();
				if (string.IsNullOrEmpty(tagKey))
					throw new McpToolException("groupBy.tag must be a tag key.");
				ctx.CheckNotHiddenTag(tagKey, "groupBy");
			}
			else
			{
				switch (gb.Value<string>())
				{
					case "hash": groupBy = StatsGroupBy.Hash; break;
					case "subType": groupBy = StatsGroupBy.SubType; break;
					case "eventType": groupBy = StatsGroupBy.EventType; break;
					default: groupBy = StatsGroupBy.Folder; break;
				}
			}
			string bucket = McpArgs.GetString(args, "bucket", "none");
			long bucketMs = bucket == "hour" ? 3600000L : bucket == "day" ? 86400000L : 0;
			long? onlyNewSince = null;
			string onlyNewSinceArg = McpArgs.GetString(args, "onlyNewSince");
			if (onlyNewSinceArg != null)
				onlyNewSince = McpOutput.ParseTime(onlyNewSinceArg, "onlyNewSince");
			int limit = McpArgs.GetInt(args, "limit", 25);

			JObject result = new JObject();
			using (DB db = new DB(p.Name))
			{
				FolderStructure folders = db.GetFolderStructure();
				string defaultSince = onlyNewSince != null ? McpOutput.Iso(onlyNewSince.Value) : "30d";
				EventCriteria criteria = McpCriteria.Parse(ctx, args, folders, defaultSince, out string appliedSince);
				if (bucketMs > 0 && criteria.Since == null)
					throw new McpToolException("bucket requires a bounded time range. Pass since (e.g. \"7d\") instead of \"all\".");
				if (bucketMs == 3600000L)
				{
					long until = criteria.Until ?? BPUtil.TimeUtil.GetTimeInMsSinceEpoch();
					if (until - criteria.Since.Value > 31L * 86400000L)
						throw new McpToolException("Hourly buckets are limited to a 31-day range. Use bucket \"day\", or a shorter since/until range.");
				}
				StatsResult stats = null;
				Dictionary<long, Event> samples = null;
				McpCriteria.RunQuery(ctx, db, () =>
				{
					stats = EventQueryService.Stats(db, criteria, groupBy, tagKey, bucketMs, onlyNewSince, limit);
					samples = EventQueryService.GetEventsById(db, stats.Groups.Select(g => g.SampleEventId)).ToDictionary(e => e.EventId);
				});
				JArray groups = new JArray();
				int budget = McpOutput.ResponseBudgetChars - 1000;
				int used = 0;
				bool truncated = false;
				foreach (StatsGroupRow g in stats.Groups)
				{
					JObject o = new JObject();
					o["key"] = FormatKey(ctx, groupBy, tagKey, g.GroupKey, folders);
					o["count"] = g.Count;
					o["firstSeen"] = McpOutput.Iso(g.FirstSeen);
					o["lastSeen"] = McpOutput.Iso(g.LastSeen);
					if (onlyNewSince != null && g.FirstSeenEver != null)
						o["firstSeenEver"] = McpOutput.Iso(g.FirstSeenEver.Value);
					o["sampleEventId"] = g.SampleEventId;
					o["sampleMessagePreview"] = samples.TryGetValue(g.SampleEventId, out Event sample) ? McpOutput.Truncate(McpOutput.MessageText(sample, ctx.Redactor, true), McpOutput.PreviewChars) : "";
					if (g.Buckets != null)
						o["buckets"] = new JArray(g.Buckets.Select(b => new JObject() { ["start"] = McpOutput.Iso(b.BucketStart), ["count"] = b.Count }));
					int len = McpOutput.JsonLength(o) + 1;
					if (groups.Count > 0 && used + len > budget)
					{
						truncated = true;
						break;
					}
					used += len;
					groups.Add(o);
				}
				result["groups"] = groups;
				result["totalMatching"] = stats.TotalMatching;
				result["totalGroups"] = stats.TotalGroups;
				if (truncated)
					result["truncated"] = true;
				result["appliedSince"] = appliedSince;
				result["serverTimeUtc"] = serverTime;
			}
			return result;
		}
		private static string FormatKey(McpCallContext ctx, StatsGroupBy groupBy, string tagKey, string key, FolderStructure folders)
		{
			switch (groupBy)
			{
				case StatsGroupBy.EventType:
					return int.TryParse(key, out int et) ? ((EventType)et).ToString() : key;
				case StatsGroupBy.Folder:
					return int.TryParse(key, out int folderId) ? McpOutput.FolderPath(folders, folderId) : key;
				case StatsGroupBy.SubType:
					return McpOutput.Truncate(ctx.Redactor.RedactText(key ?? ""), 1000);
				case StatsGroupBy.Tag:
					return McpOutput.Truncate(McpOutput.StripDataUris(ctx.Redactor.RedactTag(tagKey, key ?? "")), 1000);
				default:
					return key;
			}
		}
	}

	public class PreviewFilterTool : McpTool
	{
		public override string Name { get { return "preview_filter"; } }
		public override string Title { get { return "Preview filter (dry run)"; } }
		public override string Description
		{
			get
			{
				return "Dry run of one filter, existing (filterId) or unsaved (definition), using the filter engine's own logic against existing events. Nothing is changed. "
					+ "mode \"standalone\" (default) evaluates the filter ON ITS OWN and reports how many events it matches and what its actions would do, with sample events. "
					+ "mode \"pipeline\" runs all enabled filters in order, as for new events, once without and once with this filter at its position, and reports what would change: "
					+ "moves, deletions, colors, read state, and other filters that would stop or start acting on events (e.g. because this filter's StopExecution or Delete ends processing first). "
					+ "Use pipeline mode to check a filter's interaction with the existing filters before creating, enabling or reordering it. "
					+ "Defaults to events from the last 30 days; to predict a run_filters call, pass the same folder/since/until it will use. Always preview before creating, changing or running a filter. "
					+ McpSchemas.ConditionSemantics;
			}
		}
		public override string RequiredScope { get { return ApiTokenScopes.EventsRead; } }
		public override JObject InputSchema
		{
			get
			{
				return McpSchemas.Object(new JObject()
				{
					["project"] = McpSchemas.Project(),
					["filterId"] = McpSchemas.Int("An existing filter to preview. Give exactly one of filterId and definition.", 1),
					["definition"] = McpSchemas.FilterDefinition(),
					["folder"] = McpSchemas.Folder("Only evaluate events in this folder. Default: all folders."),
					["includeSubfolders"] = McpSchemas.Bool("With folder: also evaluate events in all folders below it. Default false."),
					["since"] = McpSchemas.Time("Only evaluate events at or after this time. Default \"30d\". Pass \"all\" for no lower bound (may be slow)."),
					["until"] = McpSchemas.Time("Only evaluate events before this time. Default: no upper bound."),
					["sampleLimit"] = McpSchemas.Int("Maximum number of sample events to return (matched events in standalone mode, changed events in pipeline mode). Default 20.", 0, 100),
					["mode"] = McpSchemas.Enum("\"standalone\" (default): this filter on its own. \"pipeline\": all enabled filters in order, without vs. with this filter.", "standalone", "pipeline"),
					["position"] = McpSchemas.Position(),
					["replaceFilterId"] = McpSchemas.Int("Pipeline mode with definition: the existing filter that definition would replace (as update_filter does). The \"before\" pipeline uses the existing filter (if enabled); the \"after\" pipeline uses definition at that filter's position, unless position is given.", 1)
				}, "project");
			}
		}
		public override JObject OutputSchema
		{
			get
			{
				return Obj(new JObject()
				{
					["evaluated"] = T("integer"),
					["matched"] = T("integer"),
					["effects"] = Obj(new JObject()
					{
						["moveTo"] = Arr(Obj(new JObject() { ["path"] = T("string"), ["count"] = T("integer"), ["folderExists"] = T("boolean") }, "path", "count", "folderExists")),
						["delete"] = T("integer"),
						["setColor"] = Arr(Obj(new JObject() { ["color"] = T("string"), ["count"] = T("integer") }, "color", "count")),
						["markRead"] = T("integer"),
						["markUnread"] = T("integer"),
						["stopExecution"] = T("integer")
					}),
					["samples"] = Arr(EventSummary()),
					["pipeline"] = Arr(Obj(new JObject() { ["filterId"] = T("integer"), ["name"] = T("string"), ["candidate"] = T("boolean") }, "filterId", "name")),
					["candidateMatched"] = T("integer"),
					["changedEvents"] = T("integer"),
					["changes"] = Obj(new JObject()
					{
						["moved"] = Arr(Obj(new JObject() { ["from"] = T("string"), ["to"] = T("string"), ["count"] = T("integer") }, "from", "to", "count")),
						["newlyDeleted"] = T("integer"),
						["noLongerDeleted"] = T("integer"),
						["colorChanged"] = Arr(Obj(new JObject() { ["color"] = T("string"), ["count"] = T("integer") }, "color", "count")),
						["readStateChanged"] = Arr(Obj(new JObject() { ["to"] = T("string"), ["count"] = T("integer") }, "to", "count")),
						["filtersNoLongerApplied"] = Arr(Obj(new JObject() { ["filterId"] = T("integer"), ["name"] = T("string"), ["count"] = T("integer") }, "filterId", "count")),
						["filtersNewlyApplied"] = Arr(Obj(new JObject() { ["filterId"] = T("integer"), ["name"] = T("string"), ["count"] = T("integer") }, "filterId", "count"))
					}),
					["changedSamples"] = Arr(Obj(new JObject() { ["event"] = EventSummary(), ["before"] = T("object"), ["after"] = T("object") }, "event", "before", "after")),
					["warnings"] = Arr(T("string")),
					["evaluation"] = T("string"),
					["truncatedScan"] = T("boolean"),
					["appliedSince"] = T("string"),
					["serverTimeUtc"] = T("string")
				}, "evaluated", "warnings", "evaluation", "truncatedScan", "appliedSince", "serverTimeUtc");
			}
		}
		public override JObject Execute(McpCallContext ctx, JObject args)
		{
			Project p = ctx.RequireProject(args, false);
			bool hasId = args["filterId"] != null && args["filterId"].Type != JTokenType.Null;
			bool hasDef = args["definition"] != null && args["definition"].Type != JTokenType.Null;
			if (hasId == hasDef)
				throw new McpInvalidParamsException("Give exactly one of filterId (an existing filter) and definition (an unsaved filter).");
			int sampleLimit = McpArgs.GetInt(args, "sampleLimit", 20);
			string serverTime = McpOutput.NowIso();

			bool pipeline = McpArgs.GetString(args, "mode", "standalone") == "pipeline";
			if (!pipeline && (args["position"] != null || args["replaceFilterId"] != null))
				throw new McpInvalidParamsException("position and replaceFilterId only apply to mode \"pipeline\".");
			if (args["replaceFilterId"] != null && !hasDef)
				throw new McpInvalidParamsException("replaceFilterId requires definition (with filterId, the existing filter is previewed at its own position).");

			FullFilter full;
			EventCriteria scope = new EventCriteria();
			FolderStructure folders;
			List<FullFilter> allFilters;
			using (DB db = new DB(p.Name))
			{
				folders = db.GetFolderStructure();
				allFilters = db.GetFilters(false);
				if (hasId)
				{
					full = db.GetFilter(McpArgs.GetInt(args, "filterId", 0));
					if (full == null)
						throw new McpToolException("Filter " + args["filterId"] + " was not found in project \"" + p.Name + "\". Call list_filters to see filter IDs.");
				}
				else
					full = FilterService.ToUnsavedFullFilter(McpArgs.ParseDefinition(args["definition"]));
			}
			McpFilterOutput.CheckHiddenTags(ctx, full);
			McpCriteria.ParseTimeRange(args, "30d", scope, out string appliedSince);
			if (args["folder"] != null && args["folder"].Type != JTokenType.Null)
				scope.FolderIds = McpArgs.GetFolderIds(McpArgs.ResolveFolder(folders, args["folder"]), McpArgs.GetBool(args, "includeSubfolders", false));

			if (pipeline)
				return ExecutePipeline(ctx, args, p, full, hasId, allFilters, folders, scope, sampleLimit, appliedSince, serverTime);

			FilterPreviewResult r;
			try
			{
				r = FilterPreviewService.Preview(p.Name, full, scope, sampleLimit, ctx.QueryTimeoutSeconds);
			}
			catch (QueryCriteriaException ex)
			{
				throw new McpToolException(ex.Message);
			}

			JObject effects = new JObject();
			if (r.Effects.Moved.Count > 0)
				effects["moveTo"] = new JArray(r.Effects.Moved.OrderByDescending(kvp => kvp.Value).Select(kvp => new JObject()
				{
					["path"] = kvp.Key,
					["count"] = kvp.Value,
					["folderExists"] = !r.MoveToFolderExists.TryGetValue(kvp.Key, out bool exists) || exists
				}));
			if (r.Effects.Deleted > 0)
				effects["delete"] = r.Effects.Deleted;
			if (r.Effects.ColoredByColor.Count > 0)
				effects["setColor"] = new JArray(r.Effects.ColoredByColor.Select(kvp => new JObject() { ["color"] = kvp.Key, ["count"] = kvp.Value }));
			if (r.Effects.MarkedRead > 0)
				effects["markRead"] = r.Effects.MarkedRead;
			if (r.Effects.MarkedUnread > 0)
				effects["markUnread"] = r.Effects.MarkedUnread;
			if (r.StopExecution > 0)
				effects["stopExecution"] = r.StopExecution;

			JArray samples = new JArray();
			HashSet<long> read;
			using (DB db = new DB(p.Name))
				read = EventQueryService.GetReadEventIds(db, ctx.User.UserId, r.Samples.Select(e => e.EventId));
			int used = 0;
			int budget = McpOutput.ResponseBudgetChars - 8000;
			foreach (Event e in r.Samples)
			{
				JObject s = McpOutput.EventSummary(e, p.Name, folders, read, null, ctx.Redactor);
				int len = McpOutput.JsonLength(s) + 1;
				if (samples.Count > 0 && used + len > budget)
					break;
				used += len;
				samples.Add(s);
			}
			if (r.TruncatedScan)
				r.Warnings.Add("The scan ran out of time (" + ctx.QueryTimeoutSeconds + " s) after evaluating " + r.Evaluated + " events (newest first). Counts cover only those events. Narrow the range with since/until or folder for a complete preview.");

			return new JObject()
			{
				["evaluated"] = r.Evaluated,
				["matched"] = r.Matched,
				["effects"] = effects,
				["samples"] = samples,
				["warnings"] = new JArray(r.Warnings),
				["evaluation"] = "standalone: this filter was evaluated on its own, not in the pipeline with the project's other filters. Use mode \"pipeline\" to see how it interacts with them.",
				["truncatedScan"] = r.TruncatedScan,
				["appliedSince"] = appliedSince,
				["serverTimeUtc"] = serverTime
			};
		}
		private JObject ExecutePipeline(McpCallContext ctx, JObject args, Project p, FullFilter candidate, bool existingCandidate, List<FullFilter> allFilters, FolderStructure folders, EventCriteria scope, int sampleLimit, string appliedSince, string serverTime)
		{
			// Every filter in execution order; the "before" pipeline is the enabled ones.
			List<FullFilter> ordered = allFilters.OrderBy(f => f.filter.MyOrder).ThenBy(f => f.filter.FilterId).ToList();
			int? replaceId = existingCandidate ? candidate.filter.FilterId : McpArgs.GetNullableInt(args, "replaceFilterId");
			List<FullFilter> before = ordered.Where(f => f.filter.Enabled && !(existingCandidate && f.filter.FilterId == candidate.filter.FilterId)).ToList();

			// The "after" pipeline: every filter except the replaced one, with the candidate inserted at its position.
			List<FullFilter> others = ordered.Where(f => replaceId == null || f.filter.FilterId != replaceId.Value).ToList();
			int index;
			FilterPosition position = McpArgs.ParsePosition(args["position"]);
			if (position != null)
			{
				if (position.Kind == "first")
					index = 0;
				else if (position.Kind == "last")
					index = others.Count;
				else
				{
					int rel = others.FindIndex(f => f.filter.FilterId == position.RelativeToFilterId);
					if (rel == -1)
						throw new McpToolException("position refers to filter " + position.RelativeToFilterId + ", which does not exist" + (position.RelativeToFilterId == replaceId ? " in the pipeline (it is the filter being replaced)" : "") + ". Call list_filters to see filter IDs.");
					index = position.Kind == "before" ? rel : rel + 1;
				}
			}
			else if (replaceId != null)
			{
				index = ordered.FindIndex(f => f.filter.FilterId == replaceId.Value);
				if (index == -1)
					throw new McpToolException("Filter " + replaceId + " was not found in project \"" + p.Name + "\". Call list_filters to see filter IDs.");
			}
			else
				index = others.Count;
			others.Insert(index, candidate);
			List<FullFilter> after = others.Where(f => f == candidate || f.filter.Enabled).ToList();

			PipelinePreviewResult r;
			try
			{
				r = FilterPreviewService.PreviewPipeline(p.Name, before, after, candidate, scope, sampleLimit, ctx.QueryTimeoutSeconds);
			}
			catch (QueryCriteriaException ex)
			{
				throw new McpToolException(ex.Message);
			}

			Dictionary<int, string> names = allFilters.ToDictionary(f => f.filter.FilterId, f => f.filter.Name);
			JArray FilterCounts(Dictionary<int, long> counts)
			{
				return new JArray(counts.OrderByDescending(kvp => kvp.Value).Select(kvp =>
				{
					JObject o = new JObject() { ["filterId"] = kvp.Key };
					if (names.TryGetValue(kvp.Key, out string name))
						o["name"] = name;
					o["count"] = kvp.Value;
					return o;
				}));
			}
			JObject Outcome(PipelineOutcome o)
			{
				JObject j = new JObject();
				if (o.Deleted)
					j["deleted"] = true;
				else
				{
					j["folderPath"] = o.FolderPath;
					j["color"] = McpOutput.Color(o.Color);
					j["readStateForAllUsers"] = o.Read == true ? "read" : o.Read == false ? "unread" : "unchanged";
				}
				j["filtersMatched"] = new JArray(o.FiltersMatched.Select(id => id == candidate.filter.FilterId ? "(this filter)" : names.TryGetValue(id, out string n) ? n : ("filter " + id)));
				return j;
			}

			JObject changes = new JObject();
			if (r.Moves.Count > 0)
				changes["moved"] = new JArray(r.Moves.OrderByDescending(kvp => kvp.Value).Select(kvp => new JObject() { ["from"] = kvp.Key.Item1, ["to"] = kvp.Key.Item2, ["count"] = kvp.Value }));
			if (r.NewlyDeleted > 0)
				changes["newlyDeleted"] = r.NewlyDeleted;
			if (r.NoLongerDeleted > 0)
				changes["noLongerDeleted"] = r.NoLongerDeleted;
			if (r.ColorChanges.Count > 0)
				changes["colorChanged"] = new JArray(r.ColorChanges.Select(kvp => new JObject() { ["color"] = kvp.Key, ["count"] = kvp.Value }));
			if (r.ReadChanges.Count > 0)
				changes["readStateChanged"] = new JArray(r.ReadChanges.Select(kvp => new JObject() { ["to"] = kvp.Key, ["count"] = kvp.Value }));
			if (r.FiltersNoLongerApplied.Count > 0)
				changes["filtersNoLongerApplied"] = FilterCounts(r.FiltersNoLongerApplied);
			if (r.FiltersNewlyApplied.Count > 0)
				changes["filtersNewlyApplied"] = FilterCounts(r.FiltersNewlyApplied);

			HashSet<long> read;
			using (DB db = new DB(p.Name))
				read = EventQueryService.GetReadEventIds(db, ctx.User.UserId, r.Samples.Select(d => d.Event.EventId));
			JArray samples = new JArray();
			int used = 0;
			int budget = McpOutput.ResponseBudgetChars - 10000;
			foreach (PipelineDifference d in r.Samples)
			{
				JObject s = new JObject()
				{
					["event"] = McpOutput.EventSummary(d.Event, p.Name, folders, read, null, ctx.Redactor),
					["before"] = Outcome(d.Before),
					["after"] = Outcome(d.After)
				};
				int len = McpOutput.JsonLength(s) + 1;
				if (samples.Count > 0 && used + len > budget)
					break;
				used += len;
				samples.Add(s);
			}
			if (r.TruncatedScan)
				r.Warnings.Add("The scan ran out of time (" + ctx.QueryTimeoutSeconds + " s) after evaluating " + r.Evaluated + " events (newest first). Counts cover only those events. Narrow the range with since/until or folder for a complete preview.");

			return new JObject()
			{
				["evaluated"] = r.Evaluated,
				["candidateMatched"] = r.CandidateMatched,
				["changedEvents"] = r.ChangedEvents,
				["changes"] = changes,
				["changedSamples"] = samples,
				["pipeline"] = new JArray(after.Select(f =>
				{
					JObject o = new JObject() { ["filterId"] = f.filter.FilterId, ["name"] = f.filter.Name ?? "" };
					if (f == candidate)
						o["candidate"] = true;
					return o;
				})),
				["warnings"] = new JArray(r.Warnings),
				["evaluation"] = "pipeline: every event was run through the enabled filters in order, once without and once with this filter (as if enabled) at its position; only differences are reported. Read state changes made by MarkRead/MarkUnread apply to all users.",
				["truncatedScan"] = r.TruncatedScan,
				["appliedSince"] = appliedSince,
				["serverTimeUtc"] = serverTime
			};
		}
	}
}

using BPUtil;
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
	/// <summary>
	/// Shared code for create_filter and update_filter.
	/// </summary>
	public abstract class FilterWriteToolBase : McpTool
	{
		public override string RequiredScope { get { return ApiTokenScopes.FiltersWrite; } }
		public override bool ReadOnlyHint { get { return false; } }
		public override JObject OutputSchema
		{
			get { return Obj(new JObject() { ["filter"] = McpFilterOutput.Schema(), ["warnings"] = Arr(T("string")) }, "filter", "warnings"); }
		}
		protected FilterDefinition ParseAndValidate(McpCallContext ctx, JObject args)
		{
			FilterDefinition def = McpArgs.ParseDefinition(args["definition"]);
			List<string> problems = FilterService.Validate(def);
			if (problems.Count > 0)
				throw new McpToolException("The filter definition is invalid. Fix every problem and try again:\n- " + string.Join("\n- ", problems));
			McpFilterOutput.CheckHiddenTags(ctx, FilterService.ToUnsavedFullFilter(def));
			return def;
		}
		protected JObject BuildResult(McpCallContext ctx, DB db, string projectName, int filterId)
		{
			FullFilter full = db.GetFilter(filterId);
			db.GetFilterAppliedStats().TryGetValue(filterId, out FilterAppliedStats stats);
			return new JObject()
			{
				["filter"] = McpFilterOutput.Filter(projectName, full, stats),
				["warnings"] = new JArray(FilterService.GetStaticWarnings(full, db.GetFolderStructure()))
			};
		}
		protected static McpToolException TranslateValidation(FilterValidationException ex)
		{
			return new McpToolException("The filter could not be saved:\n- " + string.Join("\n- ", ex.Problems));
		}
	}

	public class CreateFilterTool : FilterWriteToolBase
	{
		public override string Name { get { return "create_filter"; } }
		public override string Title { get { return "Create filter"; } }
		public override bool IdempotentHint { get { return false; } }
		public override string Description
		{
			get
			{
				return "Creates a filter. Enabled filters run on every new event, in order. definition.enabled is required: unless the user asked otherwise, create filters DISABLED, check them with preview_filter, and enable them (update_filter) once the user agrees. "
					+ "The filter, its conditions and actions are saved in one transaction. Returns the saved filter (with its etag) and warnings. "
					+ McpSchemas.ConditionSemantics;
			}
		}
		public override JObject InputSchema
		{
			get
			{
				return McpSchemas.Object(new JObject()
				{
					["project"] = McpSchemas.Project(),
					["definition"] = McpSchemas.FilterDefinition(),
					["position"] = McpSchemas.Position()
				}, "project", "definition");
			}
		}
		public override JObject Execute(McpCallContext ctx, JObject args)
		{
			Project p = ctx.RequireProject(args, true);
			FilterDefinition def = ParseAndValidate(ctx, args);
			FilterPosition position = McpArgs.ParsePosition(args["position"]);
			using (DB db = new DB(p.Name))
			{
				int filterId;
				try
				{
					filterId = FilterService.Create(db, def, position, ctx.Actor);
				}
				catch (FilterValidationException ex)
				{
					throw TranslateValidation(ex);
				}
				Logger.Info("[" + p.Name + "] Filter " + filterId + " (\"" + def.name + "\") was added by \"" + ctx.Actor + "\"");
				return BuildResult(ctx, db, p.Name, filterId);
			}
		}
	}

	public class UpdateFilterTool : FilterWriteToolBase
	{
		public override string Name { get { return "update_filter"; } }
		public override string Title { get { return "Update filter"; } }
		public override string Description
		{
			get
			{
				return "Replaces a filter's whole definition (conditions and actions are added, changed, removed and reordered to match), and optionally moves it in the execution order. All changes are made in one transaction. "
					+ "Pass the etag from get_filter (or a previous write): if someone changed the filter since, the call fails with a conflict that includes the current definition and etag, so you can merge and retry. "
					+ "Preview changes with preview_filter first. " + McpSchemas.ConditionSemantics;
			}
		}
		public override JObject InputSchema
		{
			get
			{
				return McpSchemas.Object(new JObject()
				{
					["project"] = McpSchemas.Project(),
					["filterId"] = McpSchemas.Int("ID of the filter.", 1),
					["etag"] = McpSchemas.Str("The filter's etag from get_filter or the previous write.", 100),
					["definition"] = McpSchemas.FilterDefinition(),
					["position"] = McpSchemas.Position()
				}, "project", "filterId", "etag", "definition");
			}
		}
		public override JObject Execute(McpCallContext ctx, JObject args)
		{
			Project p = ctx.RequireProject(args, true);
			int filterId = McpArgs.GetInt(args, "filterId", 0);
			FilterDefinition def = ParseAndValidate(ctx, args);
			FilterPosition position = McpArgs.ParsePosition(args["position"]);
			using (DB db = new DB(p.Name))
			{
				bool found;
				try
				{
					found = FilterService.Update(db, filterId, McpArgs.GetString(args, "etag"), def, position, ctx.Actor);
				}
				catch (FilterValidationException ex)
				{
					throw TranslateValidation(ex);
				}
				catch (FilterConflictException ex)
				{
					throw Conflict(p.Name, db, ex.Current);
				}
				if (!found)
					throw new McpToolException("Filter " + filterId + " was not found in project \"" + p.Name + "\". It may have been deleted. Call list_filters to see filter IDs.");
				Logger.Info("[" + p.Name + "] Filter " + filterId + " (\"" + def.name + "\") was edited by \"" + ctx.Actor + "\"");
				return BuildResult(ctx, db, p.Name, filterId);
			}
		}
		/// <summary>
		/// Builds the conflict error, which includes the filter's current definition and etag.
		/// </summary>
		public static McpToolException Conflict(string projectName, DB db, FullFilter current)
		{
			db.GetFilterAppliedStats().TryGetValue(current.filter.FilterId, out FilterAppliedStats stats);
			JObject currentJson = McpFilterOutput.Filter(projectName, current, stats);
			return new McpToolException("Conflict: filter " + current.filter.FilterId + " was changed by someone else (last change: " + (current.filter.UpdatedBy ?? "unknown")
				+ ") since you read it, so nothing was saved. Its current definition and etag follow. Merge your changes into it, confirm with the user if the intent changed, and retry with the new etag.\n"
				+ currentJson.ToString(Newtonsoft.Json.Formatting.None));
		}
	}

	public class DeleteFilterTool : McpTool
	{
		public override string Name { get { return "delete_filter"; } }
		public override string Title { get { return "Delete filter"; } }
		public override string RequiredScope { get { return ApiTokenScopes.FiltersWrite; } }
		public override bool ReadOnlyHint { get { return false; } }
		public override bool DestructiveHint { get { return true; } }
		public override string Description
		{
			get { return "Deletes a filter (its conditions and actions too). Events it already acted on are not changed. Requires the filter's current etag; fails with a conflict if the filter was changed since you read it."; }
		}
		public override JObject InputSchema
		{
			get
			{
				return McpSchemas.Object(new JObject()
				{
					["project"] = McpSchemas.Project(),
					["filterId"] = McpSchemas.Int("ID of the filter.", 1),
					["etag"] = McpSchemas.Str("The filter's etag from get_filter.", 100)
				}, "project", "filterId", "etag");
			}
		}
		public override JObject OutputSchema
		{
			get { return Obj(new JObject() { ["deleted"] = T("boolean"), ["alreadyDeleted"] = T("boolean") }, "deleted"); }
		}
		public override JObject Execute(McpCallContext ctx, JObject args)
		{
			Project p = ctx.RequireProject(args, true);
			int filterId = McpArgs.GetInt(args, "filterId", 0);
			using (DB db = new DB(p.Name))
			{
				bool found;
				try
				{
					found = FilterService.Delete(db, filterId, McpArgs.GetString(args, "etag"));
				}
				catch (FilterConflictException ex)
				{
					throw UpdateFilterTool.Conflict(p.Name, db, ex.Current);
				}
				if (!found)
					return new JObject() { ["deleted"] = true, ["alreadyDeleted"] = true };
				Logger.Info("[" + p.Name + "] Filter " + filterId + " was deleted by \"" + ctx.Actor + "\"");
				return new JObject() { ["deleted"] = true };
			}
		}
	}

	/// <summary>
	/// Shared output of run_filters and get_job.
	/// </summary>
	public static class RunOutput
	{
		public static JObject Build(FilterRunJob.Snapshot s)
		{
			JObject o = new JObject();
			o["status"] = s.Status;
			o["jobId"] = s.JobId;
			o["evaluated"] = s.Evaluated;
			o["matched"] = s.Matched;
			o["effects"] = new JObject()
			{
				["moved"] = new JArray(s.Effects.Moved.OrderByDescending(kvp => kvp.Value).Select(kvp => new JObject() { ["path"] = kvp.Key, ["count"] = kvp.Value })),
				["deleted"] = s.Effects.Deleted,
				["colored"] = s.Effects.Colored,
				["markedRead"] = s.Effects.MarkedRead,
				["markedUnread"] = s.Effects.MarkedUnread,
				["foldersCreated"] = new JArray(s.Effects.FoldersCreated)
			};
			o["durationMs"] = s.DurationMs;
			if (s.Error != null)
				o["error"] = s.Error;
			return o;
		}
		public static JObject Schema()
		{
			return Obj(new JObject()
			{
				["status"] = new JObject() { ["type"] = "string", ["enum"] = new JArray("completed", "running", "failed") },
				["jobId"] = T("string"),
				["evaluated"] = T("integer"),
				["matched"] = T("integer"),
				["effects"] = Obj(new JObject()
				{
					["moved"] = Arr(Obj(new JObject() { ["path"] = T("string"), ["count"] = T("integer") }, "path", "count")),
					["deleted"] = T("integer"),
					["colored"] = T("integer"),
					["markedRead"] = T("integer"),
					["markedUnread"] = T("integer"),
					["foldersCreated"] = Arr(T("string"))
				}, "moved", "deleted", "colored", "markedRead", "markedUnread", "foldersCreated"),
				["durationMs"] = T("integer"),
				["error"] = T("string"),
				["note"] = T("string")
			}, "status", "evaluated", "matched", "effects", "durationMs");
		}
	}

	public class RunFiltersTool : McpTool
	{
		/// <summary>
		/// How long run_filters waits for a run to finish before returning status "running".
		/// </summary>
		public static TimeSpan WaitTime = TimeSpan.FromSeconds(25);

		public override string Name { get { return "run_filters"; } }
		public override string Title { get { return "Run filters on existing events"; } }
		public override string RequiredScope { get { return ApiTokenScopes.FiltersRun; } }
		public override bool ReadOnlyHint { get { return false; } }
		public override bool DestructiveHint { get { return true; } }
		public override bool IdempotentHint { get { return false; } }
		public override string Description
		{
			get
			{
				return "Runs filters against existing events and CHANGES them (moves, colors, read state, deletions). With filterId, runs that one filter (even if disabled); without it, runs all enabled filters in order, exactly as for new events. "
					+ "WITHOUT since, ALL events in scope are processed, which can be slow on large projects. Always run preview_filter first with the same folder/since/until (note preview_filter defaults to the last 30 days, run_filters to everything). "
					+ "Refuses to run if any filter that would run has an enabled Delete action, unless allowDelete is true (get the user's explicit consent first). "
					+ "If the run takes longer than about 25 s, returns status \"running\" with a jobId: poll get_job. Only one run per project at a time.";
			}
		}
		public override JObject InputSchema
		{
			get
			{
				return McpSchemas.Object(new JObject()
				{
					["project"] = McpSchemas.Project(),
					["filterId"] = McpSchemas.Int("Run only this filter (even if it is disabled). Omit to run all enabled filters in order.", 1),
					["folder"] = McpSchemas.Folder("Only process events in this folder. Default: all folders."),
					["includeSubfolders"] = McpSchemas.Bool("With folder: also process events in all folders below it. Default false."),
					["since"] = McpSchemas.Time("Only process events at or after this time. Default: no lower bound (all events)."),
					["until"] = McpSchemas.Time("Only process events before this time. Default: no upper bound."),
					["allowDelete"] = McpSchemas.Bool("Must be true to run filters that have an enabled Delete action. Deleted events cannot be recovered. Default false.")
				}, "project");
			}
		}
		public override JObject OutputSchema { get { return RunOutput.Schema(); } }
		public override JObject Execute(McpCallContext ctx, JObject args)
		{
			Project p = ctx.RequireProject(args, true);
			int? filterId = McpArgs.GetNullableInt(args, "filterId");
			bool allowDelete = McpArgs.GetBool(args, "allowDelete", false);
			EventCriteria scope = new EventCriteria();
			List<FullFilter> filters;
			using (DB db = new DB(p.Name))
			{
				FolderStructure folders = db.GetFolderStructure();
				McpCriteria.ParseTimeRange(args, "all", scope, out string appliedSince);
				if (args["folder"] != null && args["folder"].Type != JTokenType.Null)
					scope.FolderIds = McpArgs.GetFolderIds(McpArgs.ResolveFolder(folders, args["folder"]), McpArgs.GetBool(args, "includeSubfolders", false));
				if (filterId != null)
				{
					FullFilter full = db.GetFilter(filterId.Value);
					if (full == null)
						throw new McpToolException("Filter " + filterId + " was not found in project \"" + p.Name + "\". Call list_filters to see filter IDs.");
					// Same short-circuits as FilterEngine.RunFilterAgainstFolder: such a filter can't do anything.
					string reason = null;
					if (!full.actions.Any(a => a.Enabled && a.Operator != FilterActionType.RememberFilterApplied))
						reason = "it has no enabled actions";
					else if (full.filter.ConditionHandling != ConditionHandling.Unconditional && !full.conditions.Any(c => c.Enabled))
						reason = "it has no enabled conditions and is not Unconditional";
					if (reason != null)
					{
						JObject nothing = RunOutput.Build(new FilterRunJob.Snapshot() { Status = "completed", Effects = new FilterRunEffects() });
						nothing.Remove("jobId");
						nothing["note"] = "Nothing was run because " + reason + ".";
						return nothing;
					}
					filters = new List<FullFilter>() { full };
				}
				else
					filters = db.GetFilters(true);
			}
			if (filters.Count == 0)
			{
				JObject nothing = RunOutput.Build(new FilterRunJob.Snapshot() { Status = "completed", Effects = new FilterRunEffects() });
				nothing.Remove("jobId");
				nothing["note"] = "The project has no enabled filters.";
				return nothing;
			}
			if (!allowDelete)
			{
				List<FullFilter> deleting = filters.Where(f => f.actions.Any(a => a.Enabled && a.Operator == FilterActionType.Delete)).ToList();
				if (deleting.Count > 0)
					throw new McpToolException("Refusing to run: these filters have an enabled Delete action: " + string.Join(", ", deleting.Select(f => f.filter.FilterId + " \"" + f.filter.Name + "\""))
						+ ". Deleted events cannot be recovered. Nothing was run. Preview with preview_filter, get the user's explicit consent, then call again with allowDelete: true.");
			}
			FilterRunJob running = FilterRunService.GetRunningJob(p.Name);
			FilterRunJob job = running == null ? FilterRunService.Start(p.Name, filters, scope, ctx.Actor) : null;
			if (job == null)
			{
				running = running ?? FilterRunService.GetRunningJob(p.Name);
				throw new McpToolException("Another filter run is already in progress for project \"" + p.Name + "\"" + (running == null ? "" : " (job " + running.JobId + ")") + ". Only one run per project is allowed at a time. Poll get_job until it finishes, then try again.");
			}
			job.WaitForCompletion(WaitTime);
			FilterRunJob.Snapshot s = job.GetSnapshot();
			JObject result = RunOutput.Build(s);
			if (s.Status == "running")
				result["note"] = "The run continues in the background. Poll get_job with this jobId.";
			return result;
		}
	}

	public class GetJobTool : McpTool
	{
		public override string Name { get { return "get_job"; } }
		public override string Title { get { return "Get filter run status"; } }
		public override string RequiredScope { get { return ApiTokenScopes.FiltersRun; } }
		public override string Description
		{
			get { return "Returns the status and progress of a run_filters job: \"running\", \"completed\" or \"failed\". Jobs are kept for at least an hour after they finish, but not across server restarts."; }
		}
		public override JObject InputSchema
		{
			get { return McpSchemas.Object(new JObject() { ["jobId"] = McpSchemas.Str("jobId returned by run_filters.", 100) }, "jobId"); }
		}
		public override JObject OutputSchema { get { return RunOutput.Schema(); } }
		public override JObject Execute(McpCallContext ctx, JObject args)
		{
			string jobId = McpArgs.GetString(args, "jobId");
			FilterRunJob job = FilterRunService.GetJob(jobId);
			Project p = job == null ? null : Settings.data.GetProject(job.ProjectName);
			if (job == null || p == null || !ctx.CanWrite(p))
				throw new McpToolException("Job \"" + McpOutput.Truncate(jobId, 100) + "\" was not found. Jobs are forgotten an hour or two after they finish and when the server restarts.");
			ctx.ProjectName = p.Name;
			return RunOutput.Build(job.GetSnapshot());
		}
	}
}

using BPUtil;
using ErrorTrackerServer.Database.Project.Model;
using ErrorTrackerServer.Filtering;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ErrorTrackerServer.Services
{
	#region Preview
	/// <summary>
	/// The result of <see cref="FilterPreviewService.Preview"/>.
	/// </summary>
	public class FilterPreviewResult
	{
		public long Evaluated;
		public long Matched;
		/// <summary>
		/// What the filter's actions would do to the matched events.
		/// </summary>
		public FilterRunEffects Effects = new FilterRunEffects();
		/// <summary>
		/// For each MoveTo destination path in <see cref="FilterRunEffects.Moved"/>, whether the folder exists already.
		/// </summary>
		public Dictionary<string, bool> MoveToFolderExists = new Dictionary<string, bool>();
		/// <summary>
		/// Number of matched events whose processing would end with a StopExecution action.
		/// </summary>
		public long StopExecution;
		/// <summary>
		/// Matched events, newest first, up to the sample limit.
		/// </summary>
		public List<Event> Samples = new List<Event>();
		public List<string> Warnings = new List<string>();
		/// <summary>
		/// True if the scan stopped early because it ran out of time.
		/// </summary>
		public bool TruncatedScan;
	}
	/// <summary>
	/// Records what a single filter would do to a single event, for <see cref="FilterPreviewService"/>.
	/// </summary>
	internal class PreviewRecorder : IFilterActionSink
	{
		private int originalFolderId;
		private uint originalColor;
		private int? moveFolderId;
		private string movePath;
		private bool deleted;
		/// <summary>
		/// True if the current event would be deleted.
		/// </summary>
		public bool Deleted { get { return deleted; } }
		private uint? color;
		private bool? read;
		/// <summary>
		/// The folder the event was in before evaluation began.
		/// </summary>
		public int OriginalFolderId { get { return originalFolderId; } }
		/// <summary>
		/// The color the event had before evaluation began.
		/// </summary>
		public uint OriginalColor { get { return originalColor; } }
		/// <summary>
		/// Destination of the last MoveTo action (-1 if the folder doesn't exist yet), or null if none.
		/// </summary>
		public int? MoveFolderId { get { return moveFolderId; } }
		/// <summary>
		/// Absolute path of the destination of the last MoveTo action, or null if none.
		/// </summary>
		public string MovePath { get { return movePath; } }
		/// <summary>
		/// Color set by the last SetColor action, or null if none.
		/// </summary>
		public uint? Color { get { return color; } }
		/// <summary>
		/// Read state set by the last MarkRead/MarkUnread action, or null if none.
		/// </summary>
		public bool? Read { get { return read; } }

		public void Begin(Event e)
		{
			originalFolderId = e.FolderId;
			originalColor = e.Color;
			moveFolderId = null;
			movePath = null;
			deleted = false;
			color = null;
			read = null;
		}
		public void MoveEventTo(Event ev, int folderId, string folderPath)
		{
			moveFolderId = folderId;
			movePath = folderPath;
		}
		public void DeleteEvent(Event ev)
		{
			deleted = true;
		}
		public void SetEventColor(Event ev, uint color)
		{
			this.color = color;
		}
		public void SetReadState(Event ev, bool read)
		{
			this.read = read;
		}
		public void RememberFilterApplied(Event ev, int filterId, long date)
		{
		}
		/// <summary>
		/// Adds the current event's recorded actions to the totals, counting them the way <see cref="DeferredActionCollection"/> does: a deleted event is only counted as deleted.
		/// </summary>
		public void End(FilterPreviewResult result)
		{
			if (deleted)
			{
				result.Effects.Deleted++;
				return;
			}
			if (moveFolderId != null && (moveFolderId.Value < 0 || moveFolderId.Value != originalFolderId))
			{
				result.Effects.AddMoved(movePath, 1);
				result.MoveToFolderExists[movePath] = moveFolderId.Value >= 0;
			}
			if (color != null && color.Value != originalColor)
				result.Effects.AddColored(color.Value, 1);
			if (read == true)
				result.Effects.MarkedRead++;
			else if (read == false)
				result.Effects.MarkedUnread++;
		}
	}
	/// <summary>
	/// Dry runs of filters.  Nothing is written to the database.
	/// </summary>
	public static partial class FilterPreviewService
	{
		/// <summary>
		/// Number of events loaded per query.
		/// </summary>
		public const int BatchSize = 500;
		/// <summary>
		/// Evaluates one filter, on its own, against the events selected by the scope, using the filter engine's own condition and action logic.
		/// </summary>
		/// <param name="projectName">Project name.</param>
		/// <param name="full">Filter to evaluate (it need not be saved, or enabled).</param>
		/// <param name="scope">Which events to evaluate.</param>
		/// <param name="sampleLimit">Maximum number of matched events to return as samples.</param>
		/// <param name="timeoutSeconds">Time budget for the whole scan.  When it runs out, the result covers the events evaluated so far.</param>
		/// <returns></returns>
		public static FilterPreviewResult Preview(string projectName, FullFilter full, EventCriteria scope, int sampleLimit, int timeoutSeconds)
		{
			FilterPreviewResult result = new FilterPreviewResult();
			PreviewRecorder recorder = new PreviewRecorder();
			Stopwatch sw = Stopwatch.StartNew();
			long budgetMs = Math.Max(1, timeoutSeconds) * 1000L;
			FilterCondition[] invertedTagConditions = full.filter.ConditionHandling == ConditionHandling.Unconditional
				? new FilterCondition[0]
				: full.conditions.Where(c => c.Enabled && c.Invert && EventQueryService.IsTagKey(c.TagKey)).ToArray();
			Dictionary<FilterCondition, long> missingTagCounts = invertedTagConditions.ToDictionary(c => c, c => 0L);

			using (FilterEngine fe = new FilterEngine(projectName, recorder))
			{
				result.Warnings.AddRange(FilterService.GetStaticWarnings(full, fe.GetFolderStructure()));
				EventKeyset cursor = null;
				while (true)
				{
					long remainingMs = budgetMs - sw.ElapsedMilliseconds;
					if (remainingMs <= 0)
					{
						result.TruncatedScan = true;
						break;
					}
					List<Event> batch = null;
					try
					{
						fe.db.RunWithStatementTimeout((int)Math.Ceiling(remainingMs / 1000.0), () =>
						{
							batch = EventQueryService.Search(fe.db, scope, cursor, BatchSize);
						});
					}
					catch (Exception ex) when (DB.IsStatementTimeout(ex))
					{
						result.TruncatedScan = true;
						break;
					}
					foreach (Event e in batch)
					{
						recorder.Begin(e);
						FilterEvalOutcome outcome = fe.EvaluateFilterAgainstEvent(full, e, false);
						// The engine updates FolderId and Color in memory as actions run; restore them so samples show the event as it is.
						e.FolderId = recorder.OriginalFolderId;
						e.Color = recorder.OriginalColor;
						result.Evaluated++;
						if (outcome.Matched)
						{
							result.Matched++;
							recorder.End(result);
							// A Delete also stops processing; it is counted under "delete" only.
							if (outcome.StopProcessing && !recorder.Deleted)
								result.StopExecution++;
							foreach (FilterCondition c in invertedTagConditions)
								if (!e.TryGetTag(c.TagKey, out string ignored))
									missingTagCounts[c]++;
							if (result.Samples.Count < sampleLimit)
								result.Samples.Add(e);
						}
					}
					if (batch.Count < BatchSize)
						break;
					Event last = batch[batch.Count - 1];
					cursor = new EventKeyset() { Date = last.Date, EventId = last.EventId };
				}
				if (fe.RegexTimeoutCount > 0)
					result.Warnings.Add("A regular expression timed out (" + FilterEngine.RegexMatchTimeout.TotalMilliseconds + " ms limit) on " + fe.RegexTimeoutCount + " event(s). The filter skips those events. Simplify the pattern.");
				if (fe.FilterErrorCount > 0)
					result.Warnings.Add("The filter failed with an error on " + fe.FilterErrorCount + " event(s) and would take no action on them. Check that every regex compiles.");
			}
			foreach (KeyValuePair<FilterCondition, long> kvp in missingTagCounts)
			{
				if (kvp.Value > 0)
					result.Warnings.Add(kvp.Value + " of the " + result.Matched + " matched events do not have the tag \"" + kvp.Key.TagKey + "\" at all. They match only because the inverted condition treats a missing tag as an empty string.");
			}
			return result;
		}
	}
	#endregion

	#region Pipeline preview
	/// <summary>
	/// What all the filters in a pipeline do to one event.
	/// </summary>
	public class PipelineOutcome
	{
		public bool Deleted;
		/// <summary>
		/// The event's folder after the pipeline (absolute path).
		/// </summary>
		public string FolderPath;
		/// <summary>
		/// The event's color after the pipeline.
		/// </summary>
		public uint Color;
		/// <summary>
		/// The read state set for all users by MarkRead/MarkUnread, or null if unchanged.
		/// </summary>
		public bool? Read;
		/// <summary>
		/// IDs of the filters whose conditions matched, in execution order.
		/// </summary>
		public List<int> FiltersMatched = new List<int>();

		public bool SameAs(PipelineOutcome o)
		{
			return Deleted == o.Deleted && FolderPath == o.FolderPath && Color == o.Color && Read == o.Read && FiltersMatched.SequenceEqual(o.FiltersMatched);
		}
	}
	/// <summary>
	/// One event whose outcome differs between the two pipelines.
	/// </summary>
	public class PipelineDifference
	{
		public Event Event;
		public PipelineOutcome Before;
		public PipelineOutcome After;
	}
	/// <summary>
	/// The result of <see cref="FilterPreviewService.PreviewPipeline"/>.
	/// </summary>
	public class PipelinePreviewResult
	{
		public long Evaluated;
		/// <summary>
		/// Number of events the candidate filter's conditions matched in the "after" pipeline (it may never see events which an earlier filter deleted or stopped).
		/// </summary>
		public long CandidateMatched;
		/// <summary>
		/// Number of events whose outcome differs.
		/// </summary>
		public long ChangedEvents;
		/// <summary>
		/// Folder changes, keyed by (before path, after path).
		/// </summary>
		public Dictionary<Tuple<string, string>, long> Moves = new Dictionary<Tuple<string, string>, long>();
		public long NewlyDeleted;
		public long NoLongerDeleted;
		/// <summary>
		/// Color changes, keyed by the new "#RRGGBB" color.
		/// </summary>
		public Dictionary<string, long> ColorChanges = new Dictionary<string, long>();
		/// <summary>
		/// Read state changes, keyed by the new state: "read", "unread" or "unchanged".
		/// </summary>
		public Dictionary<string, long> ReadChanges = new Dictionary<string, long>();
		/// <summary>
		/// Other filters which would no longer match (act on) some events, with counts.
		/// </summary>
		public Dictionary<int, long> FiltersNoLongerApplied = new Dictionary<int, long>();
		/// <summary>
		/// Other filters which would newly match (act on) some events, with counts.
		/// </summary>
		public Dictionary<int, long> FiltersNewlyApplied = new Dictionary<int, long>();
		public List<PipelineDifference> Samples = new List<PipelineDifference>();
		public List<string> Warnings = new List<string>();
		public bool TruncatedScan;
	}

	/// <summary>
	/// Dry runs of filters.  Nothing is written to the database.
	/// </summary>
	public static partial class FilterPreviewService
	{
		/// <summary>
		/// <para>Compares two filter pipelines on existing events, as a dry run: "before" (normally the project's enabled filters) and "after" (the same with the candidate filter added or replacing another).  Each event is evaluated by both pipelines exactly as an incoming event would be, and the differences are counted.</para>
		/// </summary>
		/// <param name="projectName">Project name.</param>
		/// <param name="before">Filters of the "before" pipeline, in execution order.</param>
		/// <param name="after">Filters of the "after" pipeline, in execution order.  Every filter in it runs, whether enabled or not.</param>
		/// <param name="candidate">The candidate filter (an element of <paramref name="after"/>).</param>
		/// <param name="scope">Which events to evaluate.</param>
		/// <param name="sampleLimit">Maximum number of changed events to return.</param>
		/// <param name="timeoutSeconds">Time budget for the whole scan.</param>
		/// <returns></returns>
		public static PipelinePreviewResult PreviewPipeline(string projectName, List<FullFilter> before, List<FullFilter> after, FullFilter candidate, EventCriteria scope, int sampleLimit, int timeoutSeconds)
		{
			PipelinePreviewResult result = new PipelinePreviewResult();
			PreviewRecorder recorder = new PreviewRecorder();
			Stopwatch sw = Stopwatch.StartNew();
			long budgetMs = Math.Max(1, timeoutSeconds) * 1000L;
			int candidateIndex = after.IndexOf(candidate);
			using (FilterEngine fe = new FilterEngine(projectName, recorder))
			{
				FolderStructure folders = fe.GetFolderStructure();
				result.Warnings.AddRange(FilterService.GetStaticWarnings(candidate, folders).Where(w => !w.StartsWith("The filter is disabled")));
				if (!candidate.filter.Enabled)
					result.Warnings.Add("The candidate filter is disabled. It was evaluated as if it were enabled, to show what enabling it would change.");
				EventKeyset cursor = null;
				while (true)
				{
					long remainingMs = budgetMs - sw.ElapsedMilliseconds;
					if (remainingMs <= 0)
					{
						result.TruncatedScan = true;
						break;
					}
					List<Event> batch = null;
					try
					{
						fe.db.RunWithStatementTimeout((int)Math.Ceiling(remainingMs / 1000.0), () =>
						{
							batch = EventQueryService.Search(fe.db, scope, cursor, BatchSize);
						});
					}
					catch (Exception ex) when (DB.IsStatementTimeout(ex))
					{
						result.TruncatedScan = true;
						break;
					}
					foreach (Event e in batch)
					{
						if (sw.ElapsedMilliseconds > budgetMs)
						{
							result.TruncatedScan = true;
							break;
						}
						PipelineOutcome b = RunPipeline(fe, recorder, before, CloneEvent(e), folders, -1, out bool ignored);
						PipelineOutcome a = RunPipeline(fe, recorder, after, CloneEvent(e), folders, candidateIndex, out bool candidateMatched);
						result.Evaluated++;
						if (candidateMatched)
							result.CandidateMatched++;
						if (a.SameAs(b))
							continue;
						result.ChangedEvents++;
						if (b.FolderPath != a.FolderPath && !a.Deleted && !b.Deleted)
							Increment(result.Moves, new Tuple<string, string>(b.FolderPath, a.FolderPath));
						if (a.Deleted && !b.Deleted)
							result.NewlyDeleted++;
						if (b.Deleted && !a.Deleted)
							result.NoLongerDeleted++;
						if (a.Color != b.Color && !a.Deleted)
							Increment(result.ColorChanges, "#" + (a.Color & 0xFFFFFF).ToString("X6"));
						if (a.Read != b.Read && !a.Deleted)
							Increment(result.ReadChanges, a.Read == true ? "read" : a.Read == false ? "unread" : "unchanged");
						foreach (int id in b.FiltersMatched.Except(a.FiltersMatched).Where(id => id != candidate.filter.FilterId))
							Increment(result.FiltersNoLongerApplied, id);
						foreach (int id in a.FiltersMatched.Except(b.FiltersMatched).Where(id => id != candidate.filter.FilterId))
							Increment(result.FiltersNewlyApplied, id);
						if (result.Samples.Count < sampleLimit)
							result.Samples.Add(new PipelineDifference() { Event = e, Before = b, After = a });
					}
					if (result.TruncatedScan || batch.Count < BatchSize)
						break;
					Event last = batch[batch.Count - 1];
					cursor = new EventKeyset() { Date = last.Date, EventId = last.EventId };
				}
				if (fe.RegexTimeoutCount > 0)
					result.Warnings.Add("A regular expression timed out (" + FilterEngine.RegexMatchTimeout.TotalMilliseconds + " ms limit) " + fe.RegexTimeoutCount + " time(s). The filter containing it skips those events.");
				if (fe.FilterErrorCount > 0)
					result.Warnings.Add("Filters failed with an error " + fe.FilterErrorCount + " time(s) and took no action on those events. Check that every regex compiles.");
			}
			return result;
		}
		/// <summary>
		/// Runs the filters against the event (a copy, which is modified) in order, as for an incoming event, and returns the outcome.
		/// </summary>
		private static PipelineOutcome RunPipeline(FilterEngine fe, PreviewRecorder recorder, List<FullFilter> filters, Event e, FolderStructure folders, int candidateIndex, out bool candidateMatched)
		{
			candidateMatched = false;
			PipelineOutcome o = new PipelineOutcome();
			recorder.Begin(e);
			for (int i = 0; i < filters.Count; i++)
			{
				FilterEvalOutcome outcome = fe.EvaluateFilterAgainstEvent(filters[i], e, false);
				if (outcome.Matched)
				{
					o.FiltersMatched.Add(filters[i].filter.FilterId);
					if (i == candidateIndex)
						candidateMatched = true;
				}
				if (outcome.StopProcessing)
					break;
			}
			o.Deleted = recorder.Deleted;
			o.FolderPath = recorder.MoveFolderId != null ? recorder.MovePath : FolderPath(folders, recorder.OriginalFolderId);
			o.Color = recorder.Color ?? recorder.OriginalColor;
			o.Read = recorder.Read;
			return o;
		}
		private static string FolderPath(FolderStructure folders, int folderId)
		{
			return folders.TryGetNode(folderId, out FolderStructure node) ? node.AbsolutePath : "(unknown folder " + folderId + ")";
		}
		private static void Increment<T>(Dictionary<T, long> dict, T key)
		{
			dict.TryGetValue(key, out long n);
			dict[key] = n + 1;
		}
		/// <summary>
		/// Copies an event (with its tags) so that a filter run can modify the copy.
		/// </summary>
		public static Event CloneEvent(Event e)
		{
			Event c = new Event()
			{
				EventId = e.EventId,
				HashValue = e.HashValue,
				FolderId = e.FolderId,
				EventType = e.EventType,
				SubType = e.SubType,
				Message = e.Message,
				MessageHtml = e.MessageHtml,
				Date = e.Date,
				Color = e.Color
			};
			foreach (Tag t in e.GetAllTags())
				c.SetTag(t.Key, t.Value);
			return c;
		}
	}
	#endregion

	#region Runs
	/// <summary>
	/// A background run of filters against existing events.  Thread-safe.
	/// </summary>
	public class FilterRunJob
	{
		public readonly string JobId;
		public readonly string ProjectName;
		public readonly string StartedBy;
		private readonly object syncLock = new object();
		private readonly ManualResetEventSlim completed = new ManualResetEventSlim(false);
		private readonly Stopwatch stopwatch = new Stopwatch();
		private string status = "running";
		private long evaluated;
		private long matched;
		private FilterRunEffects effects = new FilterRunEffects();
		private string error;
		/// <summary>
		/// When the job finished (UTC), or null while it runs.
		/// </summary>
		public DateTime? FinishedUtc { get; private set; }

		public FilterRunJob(string projectName, string startedBy)
		{
			JobId = "job_" + StringUtil.GetRandomAlphaNumericString(16);
			ProjectName = projectName;
			StartedBy = startedBy;
		}
		/// <summary>
		/// A consistent copy of the job's state.
		/// </summary>
		public class Snapshot
		{
			public string JobId;
			public string Status;
			public long Evaluated;
			public long Matched;
			public FilterRunEffects Effects;
			public long DurationMs;
			public string Error;
		}
		public Snapshot GetSnapshot()
		{
			lock (syncLock)
			{
				return new Snapshot()
				{
					JobId = JobId,
					Status = status,
					Evaluated = evaluated,
					Matched = matched,
					Effects = CopyEffects(effects),
					DurationMs = stopwatch.ElapsedMilliseconds,
					Error = error
				};
			}
		}
		/// <summary>
		/// Waits until the job finishes or the timeout elapses.  Returns true if the job finished.
		/// </summary>
		public bool WaitForCompletion(TimeSpan timeout)
		{
			return completed.Wait(timeout);
		}
		public bool IsFinished { get { return completed.IsSet; } }
		internal void Start()
		{
			stopwatch.Start();
		}
		internal void Progress(long evaluated, long matched, FilterRunEffects effects)
		{
			lock (syncLock)
			{
				this.evaluated = evaluated;
				this.matched = matched;
				this.effects = CopyEffects(effects);
			}
		}
		internal void Finish(string error)
		{
			lock (syncLock)
			{
				stopwatch.Stop();
				status = error == null ? "completed" : "failed";
				this.error = error;
				FinishedUtc = DateTime.UtcNow;
			}
			completed.Set();
		}
		private static FilterRunEffects CopyEffects(FilterRunEffects e)
		{
			return new FilterRunEffects()
			{
				Moved = new Dictionary<string, long>(e.Moved),
				Deleted = e.Deleted,
				Colored = e.Colored,
				ColoredByColor = new Dictionary<string, long>(e.ColoredByColor),
				MarkedRead = e.MarkedRead,
				MarkedUnread = e.MarkedUnread,
				FoldersCreated = new List<string>(e.FoldersCreated)
			};
		}
	}
	/// <summary>
	/// Runs filters against existing events in the background, at most one run per project at a time.
	/// </summary>
	public static class FilterRunService
	{
		/// <summary>
		/// Number of events loaded and processed per batch.  Actions are executed after each batch.
		/// </summary>
		public const int BatchSize = 500;
		/// <summary>
		/// Finished jobs are forgotten after this long.
		/// </summary>
		public static readonly TimeSpan JobRetention = TimeSpan.FromHours(2);
		private static ConcurrentDictionary<string, FilterRunJob> jobs = new ConcurrentDictionary<string, FilterRunJob>();
		private static ConcurrentDictionary<string, FilterRunJob> runningJobsByProject = new ConcurrentDictionary<string, FilterRunJob>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Returns the job that is currently running for the project, or null.
		/// </summary>
		public static FilterRunJob GetRunningJob(string projectName)
		{
			runningJobsByProject.TryGetValue(projectName, out FilterRunJob job);
			return job;
		}
		/// <summary>
		/// Returns the job with the specified ID, or null if it is unknown or was forgotten.
		/// </summary>
		public static FilterRunJob GetJob(string jobId)
		{
			PruneJobs();
			if (jobId == null)
				return null;
			jobs.TryGetValue(jobId, out FilterRunJob job);
			return job;
		}
		/// <summary>
		/// Starts running the filters, in order, against the events selected by the scope (newest first, in batches).  Returns null if a run is already in progress for the project.
		/// </summary>
		/// <param name="projectName">Project name.</param>
		/// <param name="filters">Filters to run, in execution order.  Each event is processed like an incoming event: a Delete or StopExecution action ends processing of that event.</param>
		/// <param name="scope">Which events to process.</param>
		/// <param name="startedBy">Who started the run, for logging.</param>
		/// <returns></returns>
		public static FilterRunJob Start(string projectName, List<FullFilter> filters, EventCriteria scope, string startedBy)
		{
			PruneJobs();
			FilterRunJob job = new FilterRunJob(projectName, startedBy);
			if (!runningJobsByProject.TryAdd(projectName, job))
				return null;
			jobs[job.JobId] = job;
			job.Start();
			Logger.Info("[" + projectName + "] Filter run " + job.JobId + " (filters " + string.Join(", ", filters.Select(f => f.filter.FilterId)) + ") was started by \"" + startedBy + "\"");
			Thread thread = new Thread(() => Run(job, filters, scope));
			thread.Name = "Filter run " + job.JobId;
			thread.IsBackground = true;
			thread.Start();
			return job;
		}
		private static void Run(FilterRunJob job, List<FullFilter> filters, EventCriteria scope)
		{
			string error = null;
			try
			{
				using (FilterEngine fe = new FilterEngine(job.ProjectName))
				{
					long evaluated = 0;
					long matched = 0;
					EventKeyset cursor = null;
					while (true)
					{
						List<Event> batch = EventQueryService.Search(fe.db, scope, cursor, BatchSize);
						foreach (Event e in batch)
						{
							evaluated++;
							if (fe.DeferredRunFiltersAgainstEvent(filters, e))
								matched++;
						}
						fe.ExecuteDeferredActions();
						job.Progress(evaluated, matched, fe.Effects);
						if (batch.Count < BatchSize)
							break;
						Event last = batch[batch.Count - 1];
						cursor = new EventKeyset() { Date = last.Date, EventId = last.EventId };
					}
				}
			}
			catch (Exception ex)
			{
				error = ex.Message;
				Logger.Debug(ex, "Filter run " + job.JobId + " failed.");
			}
			finally
			{
				runningJobsByProject.TryRemove(job.ProjectName, out FilterRunJob ignored);
				job.Finish(error);
				FilterRunJob.Snapshot s = job.GetSnapshot();
				Logger.Info("[" + job.ProjectName + "] Filter run " + job.JobId + " started by \"" + job.StartedBy + "\" " + s.Status + ": evaluated " + s.Evaluated + ", matched " + s.Matched
					+ ", moved " + s.Effects.Moved.Values.Sum() + ", deleted " + s.Effects.Deleted + ", colored " + s.Effects.Colored + ", marked read " + s.Effects.MarkedRead + ", marked unread " + s.Effects.MarkedUnread
					+ (error == null ? "" : ", error: " + error));
			}
		}
		private static void PruneJobs()
		{
			DateTime cutoff = DateTime.UtcNow - JobRetention;
			foreach (KeyValuePair<string, FilterRunJob> kvp in jobs)
			{
				DateTime? finished = kvp.Value.FinishedUtc;
				if (finished != null && finished.Value < cutoff)
					jobs.TryRemove(kvp.Key, out FilterRunJob ignored);
			}
		}
	}
	#endregion
}

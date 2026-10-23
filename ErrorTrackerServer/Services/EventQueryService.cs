using ErrorTrackerServer.Database.Project.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ErrorTrackerServer.Services
{
	/// <summary>
	/// Criteria for selecting events.  All criteria which are set are combined with AND.
	/// </summary>
	public class EventCriteria
	{
		/// <summary>
		/// PostgreSQL full-text search query (websearch_to_tsquery syntax) matched against the event's type, subtype, color, date and message, or against any tag value.
		/// </summary>
		public string Text;
		/// <summary>
		/// Literal, case-insensitive substring which must appear in the Message, SubType, EventType or any tag value.
		/// </summary>
		public string Contains;
		/// <summary>
		/// Conditions evaluated in SQL.  "Date" and "Folder" conditions are not supported.  Disabled conditions are ignored.
		/// </summary>
		public FilterCondition[] Conditions;
		/// <summary>
		/// If true, all <see cref="Conditions"/> must match.  If false, any one of them must match.
		/// </summary>
		public bool MatchAll = true;
		/// <summary>
		/// If not null or empty, only these event types.
		/// </summary>
		public EventType[] EventTypes;
		/// <summary>
		/// If not null, only events with this SubType (exact match, case-insensitive).
		/// </summary>
		public string SubType;
		/// <summary>
		/// If not null, only events with this HashValue.
		/// </summary>
		public string HashValue;
		/// <summary>
		/// If not null, only events in these folders.
		/// </summary>
		public int[] FolderIds;
		/// <summary>
		/// If not null, only events with Date at or after this time (inclusive), in milliseconds since the unix epoch.
		/// </summary>
		public long? Since;
		/// <summary>
		/// If not null, only events with Date before this time (exclusive), in milliseconds since the unix epoch.
		/// </summary>
		public long? Until;
		/// <summary>
		/// Tag keys (case-insensitive) whose values are ignored by <see cref="Text"/> and <see cref="Contains"/>, so that hidden tags can't be probed through them.
		/// </summary>
		public string[] ExcludedTagKeys;
	}
	/// <summary>
	/// Position after the last event of a page of events sorted newest first.
	/// </summary>
	public class EventKeyset
	{
		public long Date;
		public long EventId;
	}
	/// <summary>
	/// Position after the last group of a page of hash groups sorted by latest occurrence.
	/// </summary>
	public class HashGroupKeyset
	{
		public long LastSeen;
		public string HashValue;
	}
	/// <summary>
	/// A group of matching events sharing a HashValue.
	/// </summary>
	public class HashGroupRow
	{
		public string HashValue { get; set; }
		public long Count { get; set; }
		public long FirstSeen { get; set; }
		public long LastSeen { get; set; }
		/// <summary>
		/// The most recent matching event in the group.
		/// </summary>
		public Event Sample;
	}
	/// <summary>
	/// The folder and color of one event.
	/// </summary>
	public class EventStateRow
	{
		public long EventId { get; set; }
		public int FolderId { get; set; }
		public uint Color { get; set; }
	}
	/// <summary>
	/// What to group events by in <see cref="EventQueryService.Stats"/>.
	/// </summary>
	public enum StatsGroupBy
	{
		Hash,
		SubType,
		EventType,
		Folder,
		Tag
	}
	/// <summary>
	/// One group of <see cref="EventQueryService.Stats"/> output.
	/// </summary>
	public class StatsGroupRow
	{
		/// <summary>
		/// Raw group key: HashValue, SubType, numeric EventType, numeric FolderId, or tag value ("" if the event lacks the tag).
		/// </summary>
		public string GroupKey { get; set; }
		public long Count { get; set; }
		public long FirstSeen { get; set; }
		public long LastSeen { get; set; }
		public long SampleEventId { get; set; }
		/// <summary>
		/// The all-time first occurrence of the group key in the project, regardless of the criteria.  Only populated when requested.
		/// </summary>
		public long? FirstSeenEver { get; set; }
		/// <summary>
		/// Event counts per time bucket, ordered by bucket start.  Only populated when requested.
		/// </summary>
		public List<StatsBucketRow> Buckets;
	}
	public class StatsBucketRow
	{
		public string GroupKey { get; set; }
		public long BucketStart { get; set; }
		public long Count { get; set; }
	}
	/// <summary>
	/// The result of <see cref="EventQueryService.Stats"/>.
	/// </summary>
	public class StatsResult
	{
		public List<StatsGroupRow> Groups;
		/// <summary>
		/// Number of events matching the criteria (before onlyNewSince is applied).
		/// </summary>
		public long TotalMatching;
		/// <summary>
		/// Number of groups which passed all filtering, before the group limit was applied.
		/// </summary>
		public long TotalGroups;
	}
	/// <summary>
	/// Thrown when search criteria are invalid in a way the caller can fix.
	/// </summary>
	public class QueryCriteriaException : Exception
	{
		public QueryCriteriaException(string message) : base(message) { }
	}

	/// <summary>
	/// <para>Composable event queries for the MCP tools (and, in future, the web interface).  Every filter, the date range, ordering and limits are applied in SQL; nothing is filtered in memory.</para>
	/// <para>Every user-provided value is passed as a SQL parameter.  The only text concatenated into SQL is the project's schema name and integers produced by this class.</para>
	/// </summary>
	public static class EventQueryService
	{
		/// <summary>
		/// Tag keys which refer to base event fields rather than tags.
		/// </summary>
		private static readonly string[] reservedKeys = new string[] { "message", "subtype", "eventtype", "color", "date", "folder" };

		#region Criteria SQL
		/// <summary>
		/// Appends "WHERE ..." (or nothing, if there are no criteria) for the criteria, referring to the events table as alias "e".
		/// </summary>
		/// <param name="sql">SQL builder.</param>
		/// <param name="schema">Lower-case project schema name.</param>
		/// <param name="c">Criteria.</param>
		/// <param name="extraConditions">Additional SQL conditions (with their arguments) to AND with the criteria.</param>
		public static void AppendWhere(PetaPoco.Sql sql, string schema, EventCriteria c, params Tuple<string, object[]>[] extraConditions)
		{
			List<Tuple<string, object[]>> parts = BuildConditions(schema, c);
			parts.AddRange(extraConditions.Where(x => x != null));
			bool first = true;
			foreach (Tuple<string, object[]> part in parts)
			{
				sql.Append((first ? "WHERE (" : "AND (") + part.Item1 + ")", part.Item2);
				first = false;
			}
		}
		private static Tuple<string, object[]> P(string sql, params object[] args)
		{
			return new Tuple<string, object[]>(sql, args);
		}
		private static List<Tuple<string, object[]>> BuildConditions(string schema, EventCriteria c)
		{
			List<Tuple<string, object[]>> parts = new List<Tuple<string, object[]>>();
			string[] excluded = c.ExcludedTagKeys == null || c.ExcludedTagKeys.Length == 0 ? null : c.ExcludedTagKeys.Select(k => k.Trim().ToLower()).ToArray();
			if (c.Since != null)
				parts.Add(P("e.Date >= @0", c.Since.Value));
			if (c.Until != null)
				parts.Add(P("e.Date < @0", c.Until.Value));
			if (c.FolderIds != null)
			{
				if (c.FolderIds.Length == 0)
					parts.Add(P("false"));
				else
					parts.Add(P("e.FolderId IN (" + string.Join(",", c.FolderIds.Select(id => id.ToString())) + ")"));
			}
			if (c.EventTypes != null && c.EventTypes.Length > 0)
				parts.Add(P("e.EventType IN (" + string.Join(",", c.EventTypes.Select(t => ((int)t).ToString())) + ")"));
			if (c.SubType != null)
				parts.Add(P("lower(e.SubType) = lower(@0)", c.SubType));
			if (c.HashValue != null)
				parts.Add(P("e.HashValue = @0", c.HashValue));
			if (!string.IsNullOrWhiteSpace(c.Text))
			{
				// "@@@@" reaches PostgreSQL as its "@@" text search operator, because PetaPoco unescapes "@@" to "@".
				string textSearch = @"e.EventId IN
(
		SELECT e2.EventId
		FROM %PR.Event e2
		WHERE to_tsvector('english', %PR.EventTypeToString(e2.EventType) || ': ' || e2.SubType || ': ' || %PR.ColorToString(e2.Color) || ': ' || %PR.DateToString(e2.Date) || ': ' || e2.Message)
			@@@@ websearch_to_tsquery('english', @0)
	UNION
		SELECT t2.EventId
		FROM %PR.Tag t2
		WHERE to_tsvector('english', t2.Value) @@@@ websearch_to_tsquery('english', @0)" + (excluded == null ? "" : " AND lower(t2.Key) NOT IN (@1)") + @"
)";
				parts.Add(P(textSearch.Replace("%PR", schema), c.Text, excluded));
			}
			if (!string.IsNullOrEmpty(c.Contains))
			{
				parts.Add(P("strpos(lower(e.Message), lower(@0)) > 0"
					+ " OR strpos(lower(e.SubType), lower(@0)) > 0"
					+ " OR strpos(lower(" + schema + ".EventTypeToString(e.EventType)), lower(@0)) > 0"
					+ " OR EXISTS (SELECT 1 FROM " + schema + ".Tag tc WHERE tc.EventId = e.EventId AND strpos(lower(tc.Value), lower(@0)) > 0" + (excluded == null ? "" : " AND lower(tc.Key) NOT IN (@1)") + ")", c.Contains, excluded));
			}
			if (c.Conditions != null)
			{
				FilterCondition[] enabled = c.Conditions.Where(x => x.Enabled).ToArray();
				if (enabled.Length > 0)
				{
					List<Tuple<string, object[]>> conditionParts = enabled.Select(x => BuildCondition(schema, x)).ToList();
					// Combine the condition parts with AND/OR into one parenthesized expression, renumbering each part's parameters.
					List<object> args = new List<object>();
					List<string> clauses = new List<string>();
					foreach (Tuple<string, object[]> part in conditionParts)
					{
						string clause = part.Item1;
						// Rename @N placeholders from highest to lowest so that @1 does not clobber @10.
						for (int i = part.Item2.Length - 1; i >= 0; i--)
							clause = clause.Replace("@" + i, "@__" + (args.Count + i));
						args.AddRange(part.Item2);
						clauses.Add("(" + clause + ")");
					}
					string combined = string.Join(c.MatchAll ? " AND " : " OR ", clauses).Replace("@__", "@");
					parts.Add(P(combined, args.ToArray()));
				}
			}
			return parts;
		}
		/// <summary>
		/// Builds SQL for one condition, following the filter engine's rules: a missing tag is treated as an empty string, and Equals/StartsWith/EndsWith regexes are anchored automatically.  Regexes use PostgreSQL's dialect.
		/// </summary>
		private static Tuple<string, object[]> BuildCondition(string schema, FilterCondition condition)
		{
			if (string.IsNullOrWhiteSpace(condition.TagKey))
				return P("false"); // As in the filter engine, a condition with an empty TagKey never matches.
			string keyLower = condition.TagKey.Trim().ToLower();
			if (keyLower == "date")
				throw new QueryCriteriaException("\"Date\" conditions are not supported in searches. Use \"since\" and \"until\" instead.");
			if (keyLower == "folder")
				throw new QueryCriteriaException("\"Folder\" conditions are not supported in searches. Use the \"folder\" argument (and \"includeSubfolders\") instead.");

			List<object> args = new List<object>();
			string valueExpr;
			if (keyLower == "message")
				valueExpr = "e.Message";
			else if (keyLower == "subtype")
				valueExpr = "e.SubType";
			else if (keyLower == "eventtype")
				valueExpr = schema + ".EventTypeToString(e.EventType)";
			else if (keyLower == "color")
				valueExpr = "upper(" + schema + ".ColorToString(e.Color))";
			else
			{
				valueExpr = "COALESCE((SELECT tk.Value FROM " + schema + ".Tag tk WHERE tk.EventId = e.EventId AND lower(tk.Key) = lower(@0) LIMIT 1), '')";
				args.Add(Tag.ValidateTagKey(condition.TagKey));
			}
			string q = condition.Query ?? "";
			int qi = args.Count;
			string test;
			if (condition.Regex)
			{
				// Same anchoring rules as FilterEngine.DoRegexOperation.
				string pattern = q;
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
				test = valueExpr + " ~* @" + qi;
				args.Add(pattern);
			}
			else
			{
				switch (condition.Operator)
				{
					case FilterConditionOperator.Contains:
						test = "strpos(lower(" + valueExpr + "), lower(@" + qi + ")) > 0";
						break;
					case FilterConditionOperator.Equals:
						test = "lower(" + valueExpr + ") = lower(@" + qi + ")";
						break;
					case FilterConditionOperator.StartsWith:
						test = "left(lower(" + valueExpr + "), length(@" + qi + ")) = lower(@" + qi + ")";
						break;
					case FilterConditionOperator.EndsWith:
						test = "right(lower(" + valueExpr + "), length(@" + qi + ")) = lower(@" + qi + ")";
						break;
					default:
						throw new QueryCriteriaException("Unsupported condition operator: " + condition.Operator);
				}
				args.Add(q);
			}
			if (condition.Invert)
				test = "NOT (" + test + ")";
			return new Tuple<string, object[]>(test, args.ToArray());
		}
		/// <summary>
		/// Returns true if the tag key refers to a tag rather than a base event field.
		/// </summary>
		public static bool IsTagKey(string tagKey)
		{
			return !string.IsNullOrWhiteSpace(tagKey) && !reservedKeys.Contains(tagKey.Trim().ToLower());
		}
		#endregion

		#region Search
		/// <summary>
		/// Gets one page of matching events, newest first (Date DESC, EventId DESC), with their tags.
		/// </summary>
		/// <param name="db">Project database.</param>
		/// <param name="c">Criteria.</param>
		/// <param name="after">If not null, only events after this position (older) are returned.</param>
		/// <param name="limit">Maximum number of events to return.</param>
		/// <returns></returns>
		public static List<Event> Search(DB db, EventCriteria c, EventKeyset after, int limit)
		{
			string schema = db.ProjectNameLower;
			PetaPoco.Sql sql = PetaPoco.Sql.Builder.Append("SELECT e.*, (SELECT jsonb_object_agg(t.Key, t.Value) FROM " + schema + ".Tag t WHERE t.EventId = e.EventId) AS TagsJson FROM " + schema + ".Event e");
			AppendWhere(sql, schema, c, after == null ? null : P("(e.Date, e.EventId) < (@0, @1)", after.Date, after.EventId));
			sql.Append("ORDER BY e.Date DESC, e.EventId DESC LIMIT @0", limit);
			return db.FetchSql<EventWithTagsNullable>(sql).Cast<Event>().ToList();
		}
		/// <summary>
		/// Gets one page of hash groups (events sharing a HashValue), ordered by each group's latest matching occurrence, newest first.  Counts and dates describe matching events only.
		/// </summary>
		/// <param name="db">Project database.</param>
		/// <param name="c">Criteria.</param>
		/// <param name="after">If not null, only groups after this position are returned.</param>
		/// <param name="limit">Maximum number of groups to return.</param>
		/// <returns></returns>
		public static List<HashGroupRow> SearchGroupedByHash(DB db, EventCriteria c, HashGroupKeyset after, int limit)
		{
			string schema = db.ProjectNameLower;
			PetaPoco.Sql sql = PetaPoco.Sql.Builder.Append("SELECT e.HashValue, COUNT(*) AS Count, MIN(e.Date) AS FirstSeen, MAX(e.Date) AS LastSeen FROM " + schema + ".Event e");
			AppendWhere(sql, schema, c);
			sql.Append("GROUP BY e.HashValue");
			if (after != null)
				sql.Append("HAVING (MAX(e.Date), e.HashValue) < (@0, @1)", after.LastSeen, after.HashValue);
			sql.Append("ORDER BY MAX(e.Date) DESC, e.HashValue DESC LIMIT @0", limit);
			List<HashGroupRow> groups = db.FetchSql<HashGroupRow>(sql);
			if (groups.Count > 0)
			{
				// The sample of each group is its most recent matching event.
				PetaPoco.Sql sampleSql = PetaPoco.Sql.Builder.Append("SELECT DISTINCT ON (e.HashValue) e.*, (SELECT jsonb_object_agg(t.Key, t.Value) FROM " + schema + ".Tag t WHERE t.EventId = e.EventId) AS TagsJson FROM " + schema + ".Event e");
				AppendWhere(sampleSql, schema, c, P("e.HashValue IN (@0)", (object)groups.Select(g => g.HashValue).ToArray()));
				sampleSql.Append("ORDER BY e.HashValue, e.Date DESC, e.EventId DESC");
				Dictionary<string, Event> samples = db.FetchSql<EventWithTagsNullable>(sampleSql).ToDictionary(e => e.HashValue, e => (Event)e);
				foreach (HashGroupRow g in groups)
				{
					samples.TryGetValue(g.HashValue, out Event sample);
					g.Sample = sample;
				}
			}
			return groups;
		}
		/// <summary>
		/// Returns the subset of the given event IDs which the user has read.
		/// </summary>
		public static HashSet<long> GetReadEventIds(DB db, int userId, IEnumerable<long> eventIds)
		{
			long[] ids = eventIds.Distinct().ToArray();
			if (ids.Length == 0)
				return new HashSet<long>();
			PetaPoco.Sql sql = PetaPoco.Sql.Builder.Append("SELECT EventId FROM " + db.ProjectNameLower + ".ReadState WHERE UserId = @0 AND EventId IN (@1)", userId, ids);
			return new HashSet<long>(db.FetchSql<long>(sql));
		}
		/// <summary>
		/// Gets the events with the specified IDs, with their tags.  Events which don't exist are omitted.
		/// </summary>
		public static List<Event> GetEventsById(DB db, IEnumerable<long> eventIds)
		{
			long[] ids = eventIds.Distinct().ToArray();
			if (ids.Length == 0)
				return new List<Event>();
			string schema = db.ProjectNameLower;
			PetaPoco.Sql sql = PetaPoco.Sql.Builder.Append("SELECT e.*, (SELECT jsonb_object_agg(t.Key, t.Value) FROM " + schema + ".Tag t WHERE t.EventId = e.EventId) AS TagsJson FROM " + schema + ".Event e WHERE e.EventId IN (@0)", ids);
			return db.FetchSql<EventWithTagsNullable>(sql).Cast<Event>().ToList();
		}
		/// <summary>
		/// Returns the folder and color of each existing event among the given IDs, keyed by EventId.
		/// </summary>
		public static Dictionary<long, EventStateRow> GetEventStates(DB db, IEnumerable<long> eventIds)
		{
			long[] ids = eventIds.Distinct().ToArray();
			if (ids.Length == 0)
				return new Dictionary<long, EventStateRow>();
			PetaPoco.Sql sql = PetaPoco.Sql.Builder.Append("SELECT EventId, FolderId, Color FROM " + db.ProjectNameLower + ".Event WHERE EventId IN (@0)", ids);
			return db.FetchSql<EventStateRow>(sql).ToDictionary(r => r.EventId);
		}
		/// <summary>
		/// Returns the number of events sharing each of the given hash values (all time, all folders).
		/// </summary>
		public static Dictionary<string, long> CountByHash(DB db, IEnumerable<string> hashValues)
		{
			string[] hashes = hashValues.Distinct().ToArray();
			if (hashes.Length == 0)
				return new Dictionary<string, long>();
			PetaPoco.Sql sql = PetaPoco.Sql.Builder.Append("SELECT HashValue AS GroupKey, COUNT(*) AS Count FROM " + db.ProjectNameLower + ".Event WHERE HashValue IN (@0) GROUP BY HashValue", (object)hashes);
			return db.FetchSql<StatsGroupRow>(sql).ToDictionary(r => r.GroupKey, r => r.Count);
		}
		/// <summary>
		/// Returns the number of events in each folder and the number of those which the user has not read.
		/// </summary>
		public static Dictionary<int, Tuple<long, long>> CountEventsAndUnreadByFolder(DB db, int userId)
		{
			string schema = db.ProjectNameLower;
			PetaPoco.Sql sql = PetaPoco.Sql.Builder.Append("SELECT e.FolderId, COUNT(*) AS EventCount, COUNT(*) FILTER (WHERE NOT EXISTS (SELECT 1 FROM " + schema + ".ReadState rs WHERE rs.EventId = e.EventId AND rs.UserId = @0)) AS UnreadCount FROM " + schema + ".Event e GROUP BY e.FolderId", userId);
			return db.FetchSql<FolderCountRow>(sql).ToDictionary(r => r.FolderId, r => new Tuple<long, long>(r.EventCount, r.UnreadCount));
		}
		public class FolderCountRow
		{
			public int FolderId { get; set; }
			public long EventCount { get; set; }
			public long UnreadCount { get; set; }
		}
		#endregion

		#region Stats
		/// <summary>
		/// Groups matching events and returns the largest groups.
		/// </summary>
		/// <param name="db">Project database.</param>
		/// <param name="c">Criteria.</param>
		/// <param name="groupBy">What to group by.</param>
		/// <param name="tagKey">Tag key, if grouping by tag.</param>
		/// <param name="bucketSizeMs">If greater than 0, per-group event counts are computed for time buckets of this size, aligned to the unix epoch (UTC).</param>
		/// <param name="onlyNewSince">If not null, only groups whose all-time first occurrence is at or after this time are returned.</param>
		/// <param name="limit">Maximum number of groups, by count descending.</param>
		/// <returns></returns>
		public static StatsResult Stats(DB db, EventCriteria c, StatsGroupBy groupBy, string tagKey, long bucketSizeMs, long? onlyNewSince, int limit)
		{
			string schema = db.ProjectNameLower;
			List<object> keyArgs = new List<object>();
			string KeyExpr(string alias)
			{
				switch (groupBy)
				{
					case StatsGroupBy.Hash: return alias + ".HashValue";
					case StatsGroupBy.SubType: return alias + ".SubType";
					case StatsGroupBy.EventType: return alias + ".EventType::text";
					case StatsGroupBy.Folder: return alias + ".FolderId::text";
					case StatsGroupBy.Tag: return "COALESCE((SELECT tk.Value FROM " + schema + ".Tag tk WHERE tk.EventId = " + alias + ".EventId AND lower(tk.Key) = lower(@0) LIMIT 1), '')";
					default: throw new ArgumentException("Unsupported groupBy: " + groupBy);
				}
			}
			object[] KeyArgs()
			{
				return groupBy == StatsGroupBy.Tag ? new object[] { Tag.ValidateTagKey(tagKey) } : new object[0];
			}

			// Matching events and their group keys.  A new fragment is built for each query, because appending to a
			// PetaPoco.Sql which contains another Sql appends to (and so modifies) the contained one.
			PetaPoco.Sql Matching()
			{
				PetaPoco.Sql m = PetaPoco.Sql.Builder.Append("SELECT e.EventId, e.Date, " + KeyExpr("e") + " AS GroupKey FROM " + schema + ".Event e", KeyArgs());
				AppendWhere(m, schema, c);
				return m;
			}

			PetaPoco.Sql countSql = PetaPoco.Sql.Builder.Append("SELECT COUNT(*) FROM (").Append(Matching()).Append(") m");
			long totalMatching = db.ScalarSql<long>(countSql);

			PetaPoco.Sql groupsSql = PetaPoco.Sql.Builder
				.Append("WITH m AS (").Append(Matching()).Append("),")
				.Append("g AS (SELECT GroupKey, COUNT(*) AS Count, MIN(Date) AS FirstSeen, MAX(Date) AS LastSeen, (array_agg(EventId ORDER BY Date DESC, EventId DESC))[1] AS SampleEventId FROM m GROUP BY GroupKey)");
			if (onlyNewSince != null)
			{
				groupsSql.Append(", f AS (SELECT " + KeyExpr("e2") + " AS GroupKey, MIN(e2.Date) AS FirstSeenEver FROM " + schema + ".Event e2", KeyArgs());
				if (groupBy == StatsGroupBy.Hash)
					groupsSql.Append("WHERE e2.HashValue IN (SELECT GroupKey FROM g)");
				groupsSql.Append("GROUP BY 1)");
				groupsSql.Append("SELECT g.*, f.FirstSeenEver, COUNT(*) OVER () AS TotalGroups FROM g INNER JOIN f ON f.GroupKey = g.GroupKey WHERE f.FirstSeenEver >= @0", onlyNewSince.Value);
			}
			else
				groupsSql.Append("SELECT g.*, NULL::bigint AS FirstSeenEver, COUNT(*) OVER () AS TotalGroups FROM g");
			groupsSql.Append("ORDER BY g.Count DESC, g.LastSeen DESC, g.GroupKey LIMIT @0", limit);
			List<StatsGroupRowWithTotal> rows = db.FetchSql<StatsGroupRowWithTotal>(groupsSql);

			StatsResult result = new StatsResult();
			result.TotalMatching = totalMatching;
			result.TotalGroups = rows.Count > 0 ? rows[0].TotalGroups : 0;
			result.Groups = rows.Cast<StatsGroupRow>().ToList();

			if (bucketSizeMs > 0 && result.Groups.Count > 0)
			{
				PetaPoco.Sql bucketSql = PetaPoco.Sql.Builder
					.Append("WITH m AS (").Append(Matching()).Append(")")
					.Append("SELECT GroupKey, (Date - (((Date % @0) + @0) % @0)) AS BucketStart, COUNT(*) AS Count FROM m WHERE GroupKey IN (@1) GROUP BY 1, 2 ORDER BY 1, 2", bucketSizeMs, result.Groups.Select(g => g.GroupKey).ToArray());
				ILookup<string, StatsBucketRow> buckets = db.FetchSql<StatsBucketRow>(bucketSql).ToLookup(b => b.GroupKey);
				foreach (StatsGroupRow g in result.Groups)
					g.Buckets = buckets[g.GroupKey].ToList();
			}
			return result;
		}
		public class StatsGroupRowWithTotal : StatsGroupRow
		{
			public long TotalGroups { get; set; }
		}
		#endregion
	}
	/// <summary>
	/// Like <see cref="EventWithTags"/>, but tolerates events which have no tags (TagsJson is null).
	/// </summary>
	public class EventWithTagsNullable : Event
	{
		public string TagsJson
		{
			set
			{
				if (string.IsNullOrEmpty(value))
					return;
				Dictionary<string, string> dict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(value);
				if (dict == null)
					return;
				foreach (KeyValuePair<string, string> tag in dict)
					SetTag(tag.Key, tag.Value);
			}
		}
	}
}

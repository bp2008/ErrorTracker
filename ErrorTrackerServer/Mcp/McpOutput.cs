using BPUtil;
using ErrorTrackerServer.Database.Project.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ErrorTrackerServer.Mcp
{
	/// <summary>
	/// Formatting and size rules shared by all MCP tools.
	/// </summary>
	public static class McpOutput
	{
		/// <summary>
		/// Each tool result should stay under about this many characters of JSON.
		/// </summary>
		public const int ResponseBudgetChars = 40000;
		/// <summary>
		/// No tool result may exceed this many characters of JSON.
		/// </summary>
		public const int ResponseHardCapChars = 120000;
		/// <summary>
		/// Message previews in list views are truncated to this length.
		/// </summary>
		public const int PreviewChars = 300;
		/// <summary>
		/// Serializer settings for tool results: compact, and null values are omitted.
		/// </summary>
		public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings()
		{
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.None,
			DateParseHandling = DateParseHandling.None
		};
		/// <summary>
		/// Serializer used to convert result objects to JTokens.
		/// </summary>
		public static readonly JsonSerializer Serializer = JsonSerializer.Create(JsonSettings);

		#region Time
		/// <summary>
		/// Formats a time in milliseconds since the unix epoch as ISO 8601 UTC, e.g. "2026-10-23T14:05:09.123Z".
		/// </summary>
		public static string Iso(long msSinceEpoch)
		{
			return TimeUtil.DateTimeFromEpochMS(msSinceEpoch).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
		}
		/// <summary>
		/// The current time as ISO 8601 UTC.
		/// </summary>
		public static string NowIso()
		{
			return Iso(TimeUtil.GetTimeInMsSinceEpoch());
		}
		private static readonly Regex rxRelativeTime = new Regex(@"^\s*(\d{1,9})\s*(m|h|d|w)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		/// <summary>
		/// Parses a time argument: an ISO 8601 timestamp (UTC unless it has an offset) or a duration back from now such as "30m", "24h", "7d" or "4w".
		/// </summary>
		/// <param name="value">Argument value.</param>
		/// <param name="argName">Argument name, for the error message.</param>
		/// <returns>Milliseconds since the unix epoch.</returns>
		/// <exception cref="McpToolException">If the value can't be parsed.</exception>
		public static long ParseTime(string value, string argName)
		{
			Match m = rxRelativeTime.Match(value ?? "");
			if (m.Success)
			{
				long n = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
				long unitMs;
				switch (m.Groups[2].Value.ToLower())
				{
					case "m": unitMs = 60000L; break;
					case "h": unitMs = 3600000L; break;
					case "d": unitMs = 86400000L; break;
					default: unitMs = 7 * 86400000L; break;
				}
				return TimeUtil.GetTimeInMsSinceEpoch() - n * unitMs;
			}
			if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out DateTimeOffset dto))
				return dto.ToUnixTimeMilliseconds();
			throw new McpToolException("\"" + argName + "\" must be an ISO 8601 timestamp (e.g. \"2026-10-23T14:05:09Z\") or a duration back from now such as \"30m\", \"24h\", \"7d\" or \"4w\". It was: \"" + Truncate(value, 50) + "\".");
		}
		#endregion

		#region Text
		private static readonly Regex rxDataUri = new Regex(@"data:[a-zA-Z0-9.+/-]*(?:;[a-zA-Z0-9=.+-]+)*;base64,[A-Za-z0-9+/=\s]{16,}", RegexOptions.Compiled);
		/// <summary>
		/// Removes base64 "data:" URIs (embedded images and the like), which are useless to agents and can be huge.
		/// </summary>
		public static string StripDataUris(string text)
		{
			if (string.IsNullOrEmpty(text) || text.IndexOf("data:", StringComparison.OrdinalIgnoreCase) < 0)
				return text;
			return rxDataUri.Replace(text, "[data URI removed]");
		}
		/// <summary>
		/// Returns the event's message as plain text, converting HTML messages, removing data URIs and applying redaction.
		/// </summary>
		/// <param name="ev">Event.</param>
		/// <param name="redactor">Redaction policy.</param>
		/// <param name="forPreview">If true, line breaks are collapsed and only the start of a large HTML message is converted.</param>
		public static string MessageText(Event ev, McpRedactor redactor, bool forPreview)
		{
			string text = ev.Message ?? "";
			if (ev.MessageHtml)
				text = HtmlMessageSanitizer.ToPlainText(text, forPreview ? 8192 : int.MaxValue, !forPreview);
			text = StripDataUris(text);
			return redactor.RedactText(text);
		}
		/// <summary>
		/// Truncates a string to the maximum length.  Returns the original length via <paramref name="originalLength"/> if it was truncated, otherwise null.
		/// </summary>
		public static string Truncate(string text, int max, out int? originalLength)
		{
			originalLength = null;
			if (text == null || text.Length <= max)
				return text;
			originalLength = text.Length;
			// Don't split a surrogate pair.
			int cut = max;
			if (cut > 0 && char.IsHighSurrogate(text[cut - 1]))
				cut--;
			return text.Substring(0, cut);
		}
		/// <summary>
		/// Truncates a string to the maximum length, appending "…" if it was truncated.
		/// </summary>
		public static string Truncate(string text, int max)
		{
			string result = Truncate(text, max, out int? originalLength);
			return originalLength == null ? result : result + "…";
		}
		/// <summary>
		/// Formats an RGB color as "#RRGGBB".
		/// </summary>
		public static string Color(uint color)
		{
			return "#" + (color & 0xFFFFFF).ToString("X6");
		}
		#endregion

		#region Links
		/// <summary>
		/// Returns the configured public base URL with a trailing slash, or null if it is not configured.
		/// </summary>
		public static string GetPublicBaseUrl()
		{
			string baseUrl = Settings.data.publicBaseUrl?.Trim();
			if (string.IsNullOrEmpty(baseUrl))
				return null;
			return baseUrl.TrimEnd('/') + "/";
		}
		/// <summary>
		/// Returns a deep link to the event in the web interface, or null if no public base URL is configured.
		/// </summary>
		public static string EventUrl(string projectName, int folderId, long eventId)
		{
			string baseUrl = GetPublicBaseUrl();
			if (baseUrl == null)
				return null;
			return baseUrl + "client/home?p=" + Uri.EscapeDataString(projectName) + "&f=" + folderId + "&e=" + eventId;
		}
		/// <summary>
		/// Returns a deep link to the filter in the web interface, or null if no public base URL is configured.
		/// </summary>
		public static string FilterUrl(string projectName, int filterId)
		{
			string baseUrl = GetPublicBaseUrl();
			if (baseUrl == null)
				return null;
			return baseUrl + "client/filters/" + filterId + "?p=" + Uri.EscapeDataString(projectName);
		}
		#endregion

		#region Event summaries
		/// <summary>
		/// Builds the EventSummary object used by list views.
		/// </summary>
		/// <param name="ev">Event (with tags loaded, if <paramref name="includeTags"/> is used).</param>
		/// <param name="projectName">Project name, for the web link.</param>
		/// <param name="folders">Folder structure, for folder paths.</param>
		/// <param name="readEventIds">IDs of events the user has read.</param>
		/// <param name="includeTags">Tag values to include (truncated to <see cref="PreviewChars"/>).  Hidden tags are skipped.</param>
		/// <param name="redactor">Redaction policy.</param>
		public static JObject EventSummary(Event ev, string projectName, FolderStructure folders, HashSet<long> readEventIds, string[] includeTags, McpRedactor redactor)
		{
			JObject o = new JObject();
			o["eventId"] = ev.EventId;
			o["date"] = Iso(ev.Date);
			o["eventType"] = ev.EventType.ToString();
			o["subType"] = redactor.RedactText(ev.SubType ?? "");
			o["messagePreview"] = Truncate(MessageText(ev, redactor, true), PreviewChars);
			o["messageWasHtml"] = ev.MessageHtml;
			o["folderId"] = ev.FolderId;
			o["folderPath"] = FolderPath(folders, ev.FolderId);
			o["color"] = Color(ev.Color);
			o["hashValue"] = ev.HashValue;
			o["read"] = readEventIds != null && readEventIds.Contains(ev.EventId);
			if (includeTags != null && includeTags.Length > 0)
			{
				JObject tags = new JObject();
				foreach (string key in includeTags)
				{
					if (redactor.IsHiddenTag(key))
						continue;
					if (ev.TryGetTag(key, out string value) && value != null)
						tags[key] = Truncate(StripDataUris(redactor.RedactTag(key, value)), PreviewChars);
				}
				o["tags"] = tags;
			}
			string url = EventUrl(projectName, ev.FolderId, ev.EventId);
			if (url != null)
				o["webUrl"] = url;
			return o;
		}
		/// <summary>
		/// Returns the folder's absolute path, or "(unknown folder N)" if it doesn't exist.
		/// </summary>
		public static string FolderPath(FolderStructure folders, int folderId)
		{
			if (folders != null && folders.TryGetNode(folderId, out FolderStructure node))
				return node.AbsolutePath;
			return "(unknown folder " + folderId + ")";
		}
		#endregion

		#region Size
		/// <summary>
		/// Returns the length of the token's compact JSON serialization.
		/// </summary>
		public static int JsonLength(JToken token)
		{
			return token.ToString(Formatting.None).Length;
		}
		#endregion
	}
}

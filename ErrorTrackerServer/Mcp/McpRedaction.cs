using BPUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ErrorTrackerServer
{
	/// <summary>
	/// <para>A regular expression whose matches are replaced with "[REDACTED]" in MCP output.</para>
	/// <para>If the pattern defines a group named "secret", only the text captured by that group is replaced, so that, for example, "Cookie: " can be kept while the cookie value is removed.</para>
	/// </summary>
	public class McpRedactionRule
	{
		/// <summary>
		/// A short description of the rule.
		/// </summary>
		public string Name = "";
		/// <summary>
		/// If false, the rule is ignored.
		/// </summary>
		public bool Enabled = true;
		/// <summary>
		/// .NET regular expression.
		/// </summary>
		public string Pattern = "";
		/// <summary>
		/// Comma-separated tag keys (case-insensitive) the rule applies to.  If empty, the rule applies to every text field: messages, subtypes and all tag values.
		/// </summary>
		public string TagKeys = "";

		public McpRedactionRule() { }
		public McpRedactionRule(string name, string pattern, string tagKeys = "")
		{
			Name = name;
			Pattern = pattern;
			TagKeys = tagKeys;
		}
		/// <summary>
		/// Returns the built-in default rules.
		/// </summary>
		public static McpRedactionRule[] GetDefaults()
		{
			return new McpRedactionRule[]
			{
				new McpRedactionRule("Cookie and Authorization headers (\"Name: value\" and IIS \"HTTP_NAME:value\" forms)",
					@"(?im)(?<![\w-])(?:HTTP_)?(?:Set[-_]Cookie|Cookie|Proxy[-_]Authorization|Authorization)[ \t]*:[ \t]*(?<secret>[^\r\n]+)"),
				new McpRedactionRule("API key and token headers such as X-Api-Key",
					@"(?im)(?<![\w-])(?:HTTP_)?(?:X[-_](?:[A-Za-z0-9]+[-_]){0,4}?)?(?:Api[-_]?Key|Auth[-_]?Token|Access[-_]?Token|Session[-_]?Token|Csrf[-_]?Token|Xsrf[-_]?Token|Secret[-_]?Key)[ \t]*:[ \t]*(?<secret>[^\r\n]+)"),
				new McpRedactionRule("JSON fields named like password, secret, token or api key",
					@"(?i)""[^""\\\r\n]{0,40}?(?:password|passwd|pwd|secret|token|api[-_]?key|access[-_]?key|private[-_]?key)[^""\\\r\n]{0,40}?""\s*:\s*""(?<secret>(?:[^""\\]|\\.){0,10000})"""),
				new McpRedactionRule("Form, query string and connection string fields named like password, secret, token or api key",
					@"(?i)(?<![\w.-])[\w.-]{0,40}?(?:password|passwd|pwd|secret|token|api[-_]?key|access[-_]?key|private[-_]?key)[ \t]*=[ \t]*(?<secret>[^&\s;,""'<>]+)"),
				new McpRedactionRule("Bearer tokens",
					@"(?i)\bBearer[ \t]+(?<secret>[A-Za-z0-9\-._~+/]+=*)"),
				new McpRedactionRule("ErrorTracker API tokens",
					@"etk_[A-Za-z0-9]{20,}")
			};
		}
		/// <summary>
		/// Returns the tag keys this rule is limited to (lower case), or null if it applies to all text.
		/// </summary>
		public HashSet<string> GetTagKeySet()
		{
			if (string.IsNullOrWhiteSpace(TagKeys))
				return null;
			HashSet<string> set = new HashSet<string>(TagKeys.Split(',').Select(k => k.Trim().ToLower()).Where(k => k.Length > 0));
			return set.Count == 0 ? null : set;
		}
	}
}

namespace ErrorTrackerServer.Mcp
{
	/// <summary>
	/// Applies the administrator's redaction policy (hidden tag keys and <see cref="McpRedactionRule"/>s) to MCP output.  Redaction only changes what is returned; filters and searches still see the raw data.
	/// </summary>
	public class McpRedactor
	{
		/// <summary>
		/// Text that replaces redacted values.
		/// </summary>
		public const string Redacted = "[REDACTED]";
		/// <summary>
		/// Maximum time one rule may spend on one value.  If a rule times out, the whole value is redacted (fail closed).
		/// </summary>
		public static readonly TimeSpan RuleTimeout = TimeSpan.FromMilliseconds(250);
		/// <summary>
		/// A redactor that changes nothing, for tokens with the "sensitive:read" scope.
		/// </summary>
		public static readonly McpRedactor None = new McpRedactor();

		private class CompiledRule
		{
			public Regex Regex;
			public bool HasSecretGroup;
			public HashSet<string> TagKeys;
		}
		private readonly bool bypass;
		private readonly HashSet<string> hiddenTagKeys = new HashSet<string>();
		private readonly List<CompiledRule> rules = new List<CompiledRule>();

		private McpRedactor()
		{
			bypass = true;
		}
		/// <summary>
		/// Creates a redactor from the given policy.
		/// </summary>
		/// <param name="hiddenTagKeys">Tag keys, one per line or comma-separated.</param>
		/// <param name="rules">Rules.  Disabled rules and rules that fail to compile are skipped.</param>
		public McpRedactor(string hiddenTagKeys, IEnumerable<McpRedactionRule> rules)
		{
			bypass = false;
			foreach (string key in ParseKeyList(hiddenTagKeys))
				this.hiddenTagKeys.Add(key.ToLower());
			foreach (McpRedactionRule rule in rules ?? new McpRedactionRule[0])
			{
				if (rule == null || !rule.Enabled || string.IsNullOrEmpty(rule.Pattern))
					continue;
				try
				{
					Regex rx = new Regex(rule.Pattern, RegexOptions.CultureInvariant, RuleTimeout);
					this.rules.Add(new CompiledRule()
					{
						Regex = rx,
						HasSecretGroup = rx.GetGroupNames().Contains("secret"),
						TagKeys = rule.GetTagKeySet()
					});
				}
				catch (ArgumentException ex)
				{
					Logger.Debug(ex, "Ignoring MCP redaction rule \"" + rule.Name + "\" because its pattern is invalid.");
				}
			}
		}
		private static object cacheLock = new object();
		private static string cacheKey;
		private static McpRedactor cached;
		/// <summary>
		/// Returns the redactor for the current settings (cached until the settings change), or <see cref="None"/> if <paramref name="bypass"/> is true.
		/// </summary>
		/// <param name="bypass">True if the token has the "sensitive:read" scope.</param>
		public static McpRedactor ForCurrentSettings(bool bypass)
		{
			if (bypass)
				return None;
			string hidden = Settings.data.mcpHiddenTagKeys ?? "";
			McpRedactionRule[] ruleArr = Settings.data.mcpRedactionRules ?? new McpRedactionRule[0];
			string key = hidden + "\0" + string.Join("\0", ruleArr.Where(r => r != null).Select(r => r.Enabled + "\u0001" + r.Pattern + "\u0001" + r.TagKeys));
			lock (cacheLock)
			{
				if (cached == null || cacheKey != key)
				{
					cached = new McpRedactor(hidden, ruleArr);
					cacheKey = key;
				}
				return cached;
			}
		}
		/// <summary>
		/// Splits a list of tag keys separated by newlines or commas.
		/// </summary>
		public static IEnumerable<string> ParseKeyList(string keys)
		{
			if (string.IsNullOrWhiteSpace(keys))
				return new string[0];
			return keys.Split(new char[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).Where(k => k.Length > 0);
		}
		/// <summary>
		/// True if this redactor changes nothing.
		/// </summary>
		public bool IsBypassed { get { return bypass; } }
		/// <summary>
		/// The hidden tag keys (lower case).  Empty when bypassed.
		/// </summary>
		public IEnumerable<string> HiddenTagKeys { get { return bypass ? new string[0] : hiddenTagKeys.ToArray(); } }
		/// <summary>
		/// Returns true if the tag must be omitted from output.
		/// </summary>
		/// <param name="tagKey">Tag key (case-insensitive).</param>
		public bool IsHiddenTag(string tagKey)
		{
			if (bypass || tagKey == null)
				return false;
			return hiddenTagKeys.Contains(tagKey.Trim().ToLower());
		}
		/// <summary>
		/// Redacts a text field that is not a tag value (message, subtype).
		/// </summary>
		public string RedactText(string text)
		{
			return Apply(text, null);
		}
		/// <summary>
		/// Redacts a tag value.
		/// </summary>
		public string RedactTag(string tagKey, string value)
		{
			return Apply(value, tagKey?.Trim().ToLower() ?? "");
		}
		private string Apply(string text, string tagKeyLower)
		{
			if (bypass || string.IsNullOrEmpty(text))
				return text;
			foreach (CompiledRule rule in rules)
			{
				if (rule.TagKeys != null && (tagKeyLower == null || !rule.TagKeys.Contains(tagKeyLower)))
					continue;
				try
				{
					if (rule.HasSecretGroup)
					{
						text = rule.Regex.Replace(text, m =>
						{
							Group g = m.Groups["secret"];
							if (!g.Success || g.Length == 0)
								return m.Value;
							return m.Value.Substring(0, g.Index - m.Index) + Redacted + m.Value.Substring(g.Index - m.Index + g.Length);
						});
					}
					else
						text = rule.Regex.Replace(text, Redacted);
				}
				catch (RegexMatchTimeoutException)
				{
					return Redacted + " (redaction rule timed out)";
				}
			}
			return text;
		}
	}
}

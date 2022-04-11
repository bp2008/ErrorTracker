using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RepoDb.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Database.Project.Model
{
	/// <summary>
	/// Represents one filter condition.
	/// </summary>
	public class FilterCondition
	{
		/// <summary>
		/// Auto-incremented unique identifier for the filter condition.
		/// </summary>
		[Map("filterconditionid")]
		public int FilterConditionId { get; set; }
		/// <summary>
		/// ID of the filter this condition belongs to.
		/// </summary>
		[Map("filterid")]
		public int FilterId { get; set; }
		/// <summary>
		/// True if this filter condition is enabled.
		/// </summary>
		[Map("enabled")]
		public bool Enabled { get; set; }
		/// <summary>
		/// <para>Tag to inspect.</para>
		/// <para>
		/// <list type="bullet">
		/// <item>tag keys are case-insensitive</item>
		/// <item>empty string to match all tags</item>
		/// <item>"EventType", "SubType", "Message", "Date", "Folder", "Color" are reserved tag names referring not to tags but to base Event fields.</item>
		/// </list>
		/// </para>
		/// </summary>
		[Map("tagkey")]
		public string TagKey { get; set; } = "";
		/// <summary>
		/// The operation used by this filter condition.
		/// </summary>
		[JsonConverter(typeof(StringEnumConverter))]
		[Map("operator")]
		public FilterConditionOperator Operator { get; set; }
		/// <summary>
		/// Query to use when inspecting the tag.
		/// </summary>
		[Map("query")]
		public string Query { get; set; } = "";
		/// <summary>
		/// If true, <see cref="Query"/> is a regular expression.
		/// </summary>
		[Map("regex")]
		public bool Regex { get; set; }
		/// <summary>
		/// True if the operator's result should be negated. (e.g. "Not Equals")
		/// </summary>
		[Map("not")]
		public bool Not { get; set; }
	}

	public enum FilterConditionOperator : byte
	{
		Contains,
		Equals,
		StartsWith,
		EndsWith
	}
}

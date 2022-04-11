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
	public class Filter
	{
		/// <summary>
		/// Auto-incremented unique identifier for the filter.
		/// </summary>
		[Map("filterid")]
		public int FilterId { get; set; }
		/// <summary>
		/// Name of the filter.
		/// </summary>
		[Map("name")]
		public string Name { get; set; }
		/// <summary>
		/// True if this filter is enabled.
		/// </summary>
		[Map("enabled")]
		public bool Enabled { get; set; }
		/// <summary>
		/// Determines if and how the <see cref="FilterCondition"/> list is processed.
		/// </summary>
		[JsonConverter(typeof(StringEnumConverter))]
		[Map("conditionhandling")]
		public ConditionHandling ConditionHandling { get; set; }
		/// <summary>
		/// Integer for sorting for listing and executing the filters in a particular order (ascending).
		/// </summary>
		[Map("myorder")]
		public int MyOrder { get; set; }
	}
	public enum ConditionHandling : byte
	{
		/// <summary>
		/// The filter actions are performed if all conditions are met.
		/// </summary>
		All = 0,
		/// <summary>
		/// The filter actions are performed if any condition is met.
		/// </summary>
		Any = 1,
		/// <summary>
		/// The filter actions are performed regardless of conditions being met or not.
		/// </summary>
		Unconditional = 2
	}
	public class FullFilter
	{
		public Filter filter;
		public FilterCondition[] conditions;
		public FilterAction[] actions;
	}
	public class FilterSummary
	{
		public Filter filter;
		public uint NumConditions;
		public uint NumActions;
	}
	public class FilterItemCount
	{
		public int FilterId { get; set; }
		public uint Count { get; set; }
	}
	public class FilterOrder
	{
		public int FilterId;
		public int MyOrder;
	}
}
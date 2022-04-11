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
	/// Represents the action that is taken when a filter is triggered.
	/// </summary>
	public class FilterAction
	{
		/// <summary>
		/// Auto-incremented unique identifier for the filter action.
		/// </summary>
		[Map("filteractionid")]
		public int FilterActionId { get; set; }
		/// <summary>
		/// ID of the filter this action belongs to.
		/// </summary>
		[Map("filterid")]
		public int FilterId { get; set; }
		/// <summary>
		/// True if this filter action is enabled.
		/// </summary>
		[Map("enabled")]
		public bool Enabled { get; set; }
		[JsonConverter(typeof(StringEnumConverter))]
		[Map("operator")]
		public FilterActionType Operator { get; set; }
		/// <summary>
		/// Argument for to the action type. E.g. for a "MoveTo" operation, this argument would be the folder path to move the event to.
		/// </summary>
		[Map("argument")]
		public string Argument { get; set; } = "";
	}

	public enum FilterActionType : byte
	{
		/// <summary>
		/// This filter action moves the event to a specific folder.
		/// </summary>
		MoveTo = 0,
		/// <summary>
		/// This filter action causes the event to be automatically deleted.
		/// </summary>
		Delete = 1,
		/// <summary>
		/// This filter action assigns the event's color.
		/// </summary>
		SetColor = 2,
		/// <summary>
		/// When processing multiple filters against an event, this filter action causes all remaining unexecuted filters to be canceled. 
		/// </summary>
		StopExecution = 3,
		/// <summary>
		/// When processing multiple filters against an event, this filter action causes all remaining unexecuted filters to be canceled. 
		/// </summary>
		MarkRead = 4,
		/// <summary>
		/// When processing multiple filters against an event, this filter action causes all remaining unexecuted filters to be canceled. 
		/// </summary>
		MarkUnread = 5
	}
}

using BPUtil;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Database.Project.Model
{
	/// <summary>
	/// Data indicating that an event has been matched by a specific filter and actions were applied at a specific date.
	/// </summary>
	public class FilterApplied
	{
		/// <summary>
		/// Identifier for the event which was matched by the filter.
		/// </summary>
		public long EventId { get; set; }
		/// <summary>
		/// Identifier for the filter which matched the event.
		/// </summary>
		public int FilterId { get; set; }
		/// <summary>
		/// A timestamp indicating the date and time that the the event was matched by the filter, in milliseconds since the unix epoch.  Filters can be modified after they have matched an event such that they would not match again, but this Date will always indicate a time when the filter did match the event.
		/// </summary>
		public long Date { get; set; }
	}
	public class FilterAppliedRecord
	{
		/// <summary>
		/// Name of the filter which matched the event.
		/// </summary>
		public string FilterName { get; set; }
		/// <summary>
		/// Identifier for the filter which matched the event.
		/// </summary>
		public int FilterId { get; set; }
		/// <summary>
		/// A timestamp indicating the date and time that the the event was matched by the filter, in milliseconds since the unix epoch.  Filters can be modified after they have matched an event such that they would not match again, but this Date will always indicate a time when the filter did match the event.
		/// </summary>
		public long Date { get; set; }
	}
}

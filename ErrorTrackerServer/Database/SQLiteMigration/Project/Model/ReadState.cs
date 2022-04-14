using BPUtil;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.SQLiteMigration.Project.Model
{
	/// <summary>
	/// Data indicating that an event has been read by a user.
	/// </summary>
	public class ReadState
	{
		/// <summary>
		/// Identifier for the event which was read by a user.
		/// </summary>
		[Indexed]
		public int UserId { get; set; }
		/// <summary>
		/// Identifier for the event which was read by a user.
		/// </summary>
		[Indexed]
		public long EventId { get; set; }
	}
}

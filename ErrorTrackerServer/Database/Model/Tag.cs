using BPUtil;
using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Database.Model
{
	/// <summary>
	/// A Key/Value pair indicating an attribute of an event. Example keys: "IP Address", "User Name", "URL"
	/// </summary>
	public class Tag
	{
		/// <summary>
		/// Auto-incremented unique identifier for the Tag.
		/// </summary>
		[PrimaryKey, AutoIncrement]
		public long TagId { get; set; }
		/// <summary>
		/// ID of the event to which this tag belongs.
		/// </summary>
		[Indexed]
		public long EventId { get; set; }
		/// <summary>
		/// Key string. Case-insensitive for matching purposes.
		/// </summary>
		[NotNull]
		public string Key { get; set; }
		/// <summary>
		/// Value string.
		/// </summary>
		public string Value { get; set; }
		/// <summary>
		/// Zero-argument constructor for deserialization purposes.
		/// </summary>
		public Tag() { }
		/// <summary>
		/// Constructs a new Tag.
		/// </summary>
		/// <param name="Key">Tag Key</param>
		/// <param name="Value">Value of the tag.</param>
		public Tag(string Key, string Value)
		{
			this.Key = Key;
			this.Value = Value;
		}
	}
}

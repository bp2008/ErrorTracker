﻿using BPUtil;
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
		//[Indexed]
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
		/// <param name="Key">
		/// <para>Key string.</para>
		/// <para>Must be non-null and contain at least one alphanumeric character.</para></param>
		/// <para>Must not exactly match any of the reserved Key values "EventType", "SubType", "Message", "Date", "Folder", "Color".</para>
		/// <para>If any of these rules is violated, the Key string will be changed automatically.</para>
		/// <param name="Value">Value of the key.</param>
		public Tag(string Key, string Value)
		{
			this.Key = Key;
			this.Value = Value;
		}
		/// <summary>
		/// Trims and validates the [Key] field, replacing invalid Keys with valid ones.  Call this before inserting the Tag into the database.
		/// </summary>
		internal void ValidateKey()
		{
			if (Key == null)
				Key = "null";
			Key = Key.Trim();
			if (!StringUtil.IsPrintableName(Key))
				Key = "Undefined";
			if (Key.Equals("EventType", StringComparison.OrdinalIgnoreCase)
				|| Key.Equals("SubType", StringComparison.OrdinalIgnoreCase)
				|| Key.Equals("Message", StringComparison.OrdinalIgnoreCase)
				|| Key.Equals("Date", StringComparison.OrdinalIgnoreCase)
				|| Key.Equals("Folder", StringComparison.OrdinalIgnoreCase)
				|| Key.Equals("Color", StringComparison.OrdinalIgnoreCase))
				Key = "Tag_" + Key;
		}
	}
}

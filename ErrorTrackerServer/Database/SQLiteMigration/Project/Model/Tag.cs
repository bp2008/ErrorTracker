using BPUtil;
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

		/// <summary>
		/// Checks a tag key for validity and always returns a valid tag key.
		/// </summary>
		/// <param name="Key"></param>
		/// <returns></returns>
		public static string ValidateTagKey(string Key)
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
			Key = StringUtil.VisualizeSpecialCharacters(Key);
			if (Key.Length > 128)
				Key = Key.Substring(0, 128);
			return Key;
		}
	}
}

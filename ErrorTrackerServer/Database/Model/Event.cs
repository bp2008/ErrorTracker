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

namespace ErrorTrackerServer.Database.Model
{
	/// <summary>
	/// Represents one Event.  Do not deserialize untrusted data directly to this, as the Tag keys will not have been validated.
	/// </summary>
	public class Event
	{
		/// <summary>
		/// Auto-incremented unique identifier for the event.
		/// </summary>
		[PrimaryKey, AutoIncrement]
		public long EventId { get; set; }
		/// <summary>
		/// The hash value for this event. Events sharing the same Hash are strongly related to each other.
		/// </summary>
		[Indexed]
		[NotNull]
		public string HashValue { get; set; }
		/// <summary>
		/// ID of the folder this event is in.
		/// </summary>
		[Indexed]
		public int FolderId { get; set; }
		/// <summary>
		/// Type of event.
		/// </summary>
		[Indexed]
		[JsonConverter(typeof(StringEnumConverter))]
		public EventType EventType { get; set; }
		/// <summary>
		/// A string describing the type of event, to be used as a subtitle and a search filter.  E.g. So you can list "Info" events with SubType "AppStart".
		/// </summary>
		[Indexed]
		[NotNull]
		public string SubType { get; set; }
		/// <summary>
		/// The main body of the event. Describes what happened to cause the event.
		/// </summary>
		[NotNull]
		public string Message { get; set; }
		/// <summary>
		/// A timestamp indicating the date and time of this event, in milliseconds since the unix epoch. (assigned by the Error Tracker Client)
		/// </summary>
		[Indexed]
		public long Date { get; set; }
		/// <summary>
		/// RGB color value associated with this event.
		/// </summary>
		[JsonConverter(typeof(HexStringJsonConverter), 3)]
		public uint Color { get; set; } = 0xEBEBEB;
		/// <summary>
		/// Tags associated with the event. Keys must be non-null and contain at least one alphanumeric character. Keys must not exactly match any of the reserved Key values "EventType", "SubType", "Message", "Date", "Folder", "Color".
		/// </summary>
		[Ignore]
		[JsonProperty("Tags")]
		private Dictionary<string, Tag> _tags { get; set; }
		public override string ToString()
		{
			return EventType + ": " + SubType + ": " + Message;
		}

		/// <summary>
		/// Sets a tag. (not thread-safe)
		/// </summary>
		/// <param name="Key">
		/// <para>Key string.</para>
		/// <para>Must be non-null and contain at least one alphanumeric character.</para>
		/// <para>Must not exactly match any of the reserved Key values "EventType", "SubType", "Message", "Date", "Folder", "Color".</para>
		/// <para>If any of these rules is violated, the Key string will be changed automatically.</para>
		/// </param>
		/// <param name="Value">Value of the tag.</param>
		public void SetTag(string Key, string Value)
		{
			if (_tags == null)
				_tags = new Dictionary<string, Tag>();
			Key = ValidateTagKey(Key);
			_tags[Key.ToLower()] = new Tag(Key, Value);
		}

		/// <summary>
		/// Gets the value of the specified tag, returning true if successful. (not thread-safe)
		/// </summary>
		/// <param name="Key">Key of the tag to retrieve. Not case-sensitive.</param>
		/// <param name="Value">Value of the tag, if the tag exists.</param>
		/// <returns></returns>
		public bool TryGetTag(string Key, out string Value)
		{
			if (_tags != null)
			{
				Key = ValidateTagKey(Key);
				if (_tags.TryGetValue(Key.ToLower(), out Tag t))
				{
					Value = t.Value;
					return true;
				}
			}
			Value = null;
			return false;
		}
		/// <summary>
		/// Tries to remove the specified tag, returning true if successful. (not thread-safe)
		/// </summary>
		/// <param name="key">Key of the tag to remove. Not case-sensitive.</param>
		/// <returns></returns>
		public bool TryRemoveTag(string Key)
		{
			if (_tags == null)
				return false;
			Key = ValidateTagKey(Key);
			return _tags.Remove(Key.ToLower());
		}
		/// <summary>
		/// Builds a list of Tag instances from the tags in this event.  The tags will have their EventId fields pre-populated to match this event, but the TagId fields may be unset. (not thread-safe)
		/// </summary>
		/// <returns></returns>
		public List<Tag> GetAllTags()
		{
			if (_tags == null)
				return new List<Tag>(0);
			List<Tag> allTags = _tags.Values.ToList();
			foreach (Tag t in allTags)
				t.EventId = this.EventId;
			return allTags;
		}

		/// <summary>
		/// Checks a tag key for validity and always returns a valid tag key.
		/// </summary>
		/// <param name="Key"></param>
		/// <returns></returns>
		private static string ValidateTagKey(string Key)
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
			return Key;
		}
		public int GetTagCount()
		{
			if (_tags == null)
				return 0;
			return this._tags.Count;
		}

		/// <summary>
		/// Clears the tags collection. (not thread-safe)
		/// </summary>
		public void ClearTags()
		{
			_tags?.Clear();
		}

		/// <summary>
		/// <para>Computes the base64 encoded (without padding) MD5 hash value of this event, which is always 22 characters long.  Sets it as the value of the <see cref="HashValue" /> property.</para>
		/// <para>Returns true if the value of the Hash property changed as a result of calling this method.</para>
		/// <para>Only some of the event data is included in the hash so that similar events can be identified by sharing the hash value.</para>
		/// <para>If the hashing implementation or hashed data ever changes, the database version should be incremented and during the migration all Events should have their Hash properties recomputed.</para>
		/// </summary>
		/// <returns></returns>
		public bool ComputeHash()
		{
			const int messageChars = 250;
			string strToHash = ((int)EventType).ToString() + "\n"
				+ SubType + "\n"
				+ (Message == null || Message.Length <= messageChars ? Message : Message.Substring(messageChars));
			byte[] md5 = Hash.GetMD5Bytes(strToHash);
			string base64 = Convert.ToBase64String(md5);
			if (HashValue != base64)
			{
				HashValue = base64;
				return true;
			}
			return false;
		}
	}

	public enum EventType : byte
	{
		/// <summary>
		/// For events that indicate an error occurred.
		/// </summary>
		Error = 0,
		/// <summary>
		/// For events that do not indicate an error.  Such as "Application Started".
		/// </summary>
		Info = 1,
		/// <summary>
		/// For events intended to log information for debugging purposes.  E.g. Logging the state of an application during a specific point in execution.
		/// </summary>
		Debug = 2
	}
}

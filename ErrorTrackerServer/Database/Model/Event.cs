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
	public class Event
	{
		/// <summary>
		/// Auto-incremented unique identifier for the event.
		/// </summary>
		[PrimaryKey, AutoIncrement]
		public long EventId { get; set; }
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
		public string SubType { get; set; }
		/// <summary>
		/// The main body of the event. Describes what happened to cause the event.
		/// </summary>
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
		/// A list of tags associated with the event. Initialized to an empty list.
		/// </summary>
		[Ignore]
		public List<Tag> Tags { get; set; } = new List<Tag>();
		/// <summary>
		/// Private lazy-loaded dictionary of Tag.Key -> Tag.
		/// </summary>
		[JsonIgnore]
		private Dictionary<string, Tag> _tagLookup;
		/// <summary>
		/// Looks up a tag by its lower-case key string. The key argument must be all lower-case. The first time you call this method on an event, a Dictionary is created from all the Tags in the event.  This method is thread safe but not efficient if it results in creation of multiple dictionaries.
		/// </summary>
		/// <param name="keyLower"></param>
		/// <returns></returns>
		public Tag TagLookup(string keyLower)
		{
			if (_tagLookup == null)
				_tagLookup = Tags.ToDictionary(t => t.Key.ToLower());
			if (_tagLookup.TryGetValue(keyLower, out Tag tag))
				return tag;
			return null;
		}
		public override string ToString()
		{
			return EventType + ": " + SubType + ": " + Message;
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

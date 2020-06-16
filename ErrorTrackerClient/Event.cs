using System;
using System.Collections.Generic;
using System.Linq;

namespace ErrorTrackerClient
{
	/// <summary>
	/// Represents one event that is sent to an Error Tracker server.
	/// </summary>
	public class Event
	{
		/// <summary>
		/// Type of event.
		/// </summary>
		public EventType EventType;
		/// <summary>
		/// A string describing the type of event, to be used as a subtitle and a search filter.  E.g. So you can list "Info" events with SubType "AppStart".
		/// </summary>
		public string SubType;
		/// <summary>
		/// The main body of the event. Describes what happened to cause the event.
		/// </summary>
		public string Message;
		/// <summary>
		/// A timestamp indicating the date and time of this event, in milliseconds since the unix epoch. Assigned automatically at the start of Event construction.
		/// </summary>
		public long Date = TimeUtil.GetTimeInMsSinceEpoch();
		/// <summary>
		/// <para>DO NOT EDIT THIS DICTIONARY.</para>
		/// <para>This dictionary is public so that JSON serializers will see it. Use the methods SetTag, GetTag, etc....  I'd use JSON.NET's JsonProperty attribute on a private field, but that would require including a reference to a specific version of JSON.NET which I'm trying to avoid.</para>
		/// <para>The dictionary values here are Tag instances so they can contain the original key while the dictionary key is all lower-case.</para>
		/// </summary>
		public Dictionary<string, Tag> Private_Tags;
		/// <summary>
		/// Zero-argument constructor for deserialization.
		/// </summary>
		private Event() { }
		/// <summary>
		/// Constructs a new Event.
		/// </summary>
		/// <param name="eventType">Primary type of the event.</param>
		/// <param name="subType">Sub type of the event, useful for searching/filtering later. This is presented in the Error Tracker UI much like a title.</param>
		/// <param name="message">Message describing what happened.</param>
		public Event(EventType eventType, string subType, string message)
		{
			this.EventType = eventType;
			this.SubType = subType;
			if (string.IsNullOrWhiteSpace(message))
				message = "[Message was null or whitespace]";
			this.Message = message;
		}
		/// <summary>
		/// Sets a tag. (not thread-safe)
		/// </summary>
		/// <param name="Key">
		/// <para>Key string, case-sensitive.</para>
		/// <para>Must be non-null and contain at least one alphanumeric character.</para>
		/// <para>Must not exactly match any of the reserved Key values "EventType", "SubType", "Message", "Date", "Folder", "Color".</para>
		/// <para>If any of these rules is violated, the Key string will be changed automatically.</para>
		/// </param>
		/// <param name="Value">Value of the tag.</param>
		public void SetTag(string Key, string Value)
		{
			if (Key == null)
				throw new Exception("Tag Key cannot be null");
			Key = Key.Trim();
			if (!StringUtil.IsPrintableName(Key))
				throw new Exception("Tag Key is invalid");
			if (Key.Equals("EventType", StringComparison.OrdinalIgnoreCase)
				|| Key.Equals("SubType", StringComparison.OrdinalIgnoreCase)
				|| Key.Equals("Message", StringComparison.OrdinalIgnoreCase)
				|| Key.Equals("Date", StringComparison.OrdinalIgnoreCase)
				|| Key.Equals("Folder", StringComparison.OrdinalIgnoreCase)
				|| Key.Equals("Color", StringComparison.OrdinalIgnoreCase))
				throw new Exception("Tag Key cannot be \"" + Key + "\" (Key is reserved)");

			if (Private_Tags == null)
				Private_Tags = new Dictionary<string, Tag>();
			Private_Tags[Key.ToLower()] = new Tag(Key, Value);
		}

		/// <summary>
		/// Gets the value of the specified tag, returning true if successful. (not thread-safe)
		/// </summary>
		/// <param name="Key">Key of the tag to retrieve.</param>
		/// <param name="Value">Value of the tag, if the tag exists.</param>
		/// <returns></returns>
		public bool TryGetTag(string Key, out string Value)
		{
			if (Private_Tags != null)
			{
				if (Private_Tags.TryGetValue(Key.ToLower(), out Tag t))
				{
					Value = t.Value;
					return true;
				}
			}
			Value = null;
			return false;
		}
		/// <summary>
		/// Tries to remove the specified tag, returning true if successful.
		/// </summary>
		/// <param name="Key">Key of the tag to remove.</param>
		/// <returns></returns>
		public bool TryRemoveTag(string Key)
		{
			if (Private_Tags == null)
				return false;
			return Private_Tags.Remove(Key.ToLower());
		}
		/// <summary>
		/// Returns a list of all the tags in arbitrary order.
		/// </summary>
		/// <returns></returns>
		public List<ReadOnlyTag> GetAllTags()
		{
			if (Private_Tags == null)
				return new List<ReadOnlyTag>(0);
			return Private_Tags.Values.Select(t => new ReadOnlyTag(t.Key, t.Value)).ToList();
		}
	}

	/// <summary>
	/// Primary type of event.
	/// </summary>
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
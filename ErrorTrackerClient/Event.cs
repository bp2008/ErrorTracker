using System;
using System.Collections.Generic;

namespace ErrorTrackerClient
{
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
		/// A list of tags associated with the event. Initialized to an empty list.
		/// </summary>
		public List<Tag> Tags = new List<Tag>();
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
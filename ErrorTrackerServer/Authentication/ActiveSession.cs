using ErrorTrackerServer.Code;
using Newtonsoft.Json;
using System.Net;

namespace ErrorTrackerServer.Authentication
{
	/// <summary>
	/// An active session record from the Sessions.json file.
	/// </summary>
	public class ActiveSession
	{
		/// <summary>
		/// User name that was logged in. All lower case for effectively case-insensitive matching of user names. Null for unauthenticated sessions.
		/// </summary>
		public string UserName { get; set; }
		/// <summary>
		/// IP Address that started the session.
		/// </summary>
		[JsonConverter(typeof(IPAddressConverter))]
		public IPAddress IPAddress { get; set; }
		/// <summary>
		/// Session ID that was assigned.
		/// </summary>
		public string SessionID { get; set; }
		/// <summary>
		/// Date and time of the session's creation, in milliseconds since the unix epoch.
		/// </summary>
		public long DateCreated { get; set; }
		/// <summary>
		/// Date and time of the session's last access, in milliseconds since the unix epoch.
		/// </summary>
		public long DateTouched { get; set; }

		public ActiveSession() { }
		public ActiveSession(string userName, IPAddress ipAddress, string sessionId, long dateCreated, long dateTouched)
		{
			UserName = userName;
			IPAddress = ipAddress;
			SessionID = sessionId;
			DateCreated = dateCreated;
			DateTouched = dateTouched;
		}
	}
}

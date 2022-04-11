using RepoDb.Attributes;
using System.Net;

namespace ErrorTrackerServer.Database.Global.Model
{
	[Map("ErrorTrackerGlobal.LoginRecord")]
	public class LoginRecord
	{
		/// <summary>
		/// User name that was logged in. All lower case for effectively case-insensitive matching of user names.
		/// </summary>
		public string UserName { get; set; }
		/// <summary>
		/// IP Address that provided credentials for the login.
		/// </summary>
		public IPAddress IPAddress { get; set; }
		/// <summary>
		/// Session ID that was assigned.
		/// </summary>
		public string SessionID { get; set; }
		/// <summary>
		/// Date and time of the login, in milliseconds since the unix epoch.
		/// </summary>
		public long Date { get; set; }

		public LoginRecord() { }
		public LoginRecord(string userName, IPAddress ipAddress, string sessionId, long date)
		{
			UserName = userName;
			IPAddress = ipAddress;
			SessionID = sessionId;
			Date = date;
		}
	}
}

using SQLite;

namespace ErrorTrackerServer.Database.Global.Model
{
	public class LoginRecord
	{
		/// <summary>
		/// User name that was logged in. All lower case for effectively case-insensitive matching of user names.
		/// </summary>
		[Indexed]
		[NotNull]
		public string UserName { get; set; }
		/// <summary>
		/// IP Address that provided credentials for the login.
		/// </summary>
		[NotNull]
		public string IPAddress { get; set; }
		/// <summary>
		/// Session ID that was assigned.
		/// </summary>
		[NotNull]
		public string SessionID { get; set; }
		/// <summary>
		/// Date and time of the login, in milliseconds since the unix epoch.
		/// </summary>
		[Indexed]
		public long Date { get; set; }

		public LoginRecord() { }
		public LoginRecord(string userName, string ipAddress, string sessionId, long date)
		{
			UserName = userName;
			IPAddress = ipAddress;
			SessionID = sessionId;
			Date = date;
		}
	}
}

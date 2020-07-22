using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;

namespace ErrorTrackerServer
{
	/// <summary>
	/// Represents a session between the client and server, and persists state information.
	/// </summary>
	public class ServerSession
	{
		/// <summary>
		/// Session ID string.
		/// </summary>
		public string sid;

		/// <summary>
		/// A nonce used in authentication so the client does not have to send the password in plain text.
		/// </summary>
		public byte[] authChallenge;

		/// <summary>
		/// The name of the authenticated user, or null until authentication is successful.
		/// </summary>
		public string userName = null;

		/// <summary>
		/// Contains SessionManager.CurrentTime from the time this session was last touched by a user.  This value controls idle timeouts.
		/// </summary>
		private long lastTouched = 0;

		/// <summary>
		/// Returns true if the session has expired.
		/// </summary>
		public bool Expired
		{
			get
			{
				if (userName == null)
					return SessionManager.CurrentTime > lastTouched + SessionManager.SessionMaxUnauthenticatedTime;
				else
					return SessionManager.CurrentTime > lastTouched + SessionManager.SessionMaxIdleTime;
			}
		}
		/// <summary>
		/// Returns true if the session has been authenticated.
		/// </summary>
		private bool IsAuthenticated { get { return userName != null; } }

		/// <summary>
		/// Returns true if the session has been authenticated and is not expired.
		/// </summary>
		public bool IsAuthValid { get { return IsAuthenticated && !Expired; } }

		/// <summary>
		/// Returns true if the session has been authenticated, is not expired, and has admin privileges.
		/// </summary>
		public bool IsAdminValid
		{
			get
			{
				if (!IsAuthValid)
					return false;
				User u = GetUser();
				if (u == null)
					return false;
				return u.IsAdmin;
			}
		}

		/// <summary>
		/// Retrieves the User from Settings.data.
		/// </summary>
		/// <returns></returns>
		public User GetUser()
		{
			if (userName == null)
				return null;
			return Settings.data.GetUser(userName);
		}

		private ServerSession()
		{
			lastTouched = SessionManager.CurrentTime;
		}

		internal static ServerSession CreateUnauthenticated()
		{
			ServerSession session = new ServerSession();
			// 16 characters, each character having 62 possible values, yields (62 ^ 16 =) 47672401706823533450263330816 possible session strings.
			session.sid = StringUtil.GetRandomAlphaNumericString(16);
			session.TouchNow();
			return session;
		}

		/// <summary>
		/// Call when the session is created or used by a user.  Prevents idle-logoff for a time.
		/// Has no effect on unauthenticated or expired sessions.
		/// </summary>
		public void TouchNow()
		{
			if (userName != null && !Expired)
				lastTouched = SessionManager.CurrentTime;
		}
	}
}

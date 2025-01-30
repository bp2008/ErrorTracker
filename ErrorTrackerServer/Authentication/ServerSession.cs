using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using Newtonsoft.Json;

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
		[JsonProperty]
		public string sid { get; private set; }

		/// <summary>
		/// A nonce used in authentication so the client does not have to send the password in plain text.
		/// </summary>
		[JsonProperty]
		public byte[] authChallenge;

		/// <summary>
		/// The name of the authenticated user, or null until authentication is successful.
		/// </summary>
		[JsonProperty]
		public string userName { get; private set; } = null;

		/// <summary>
		/// Contains SessionManager.CurrentTime from the time this session was last touched by a user.  This value controls idle timeouts.
		/// </summary>
		[JsonProperty]
		public long lastTouched { get; private set; } = 0;

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
		}
		/// <summary>
		/// Creates an unauthenticated session with a random session ID and adds it to the session manager.
		/// </summary>
		/// <returns></returns>
		internal static ServerSession CreateUnauthenticated()
		{
			ServerSession session = new ServerSession();
			session.lastTouched = SessionManager.CurrentTime;
			// 16 characters, each character having 62 possible values, yields (62 ^ 16 =) 47672401706823533450263330816 possible session strings.
			session.sid = StringUtil.GetRandomAlphaNumericString(16);
			SessionManager.AddSession(session);
			return session;
		}

		/// <summary>
		/// <para>Call when the session is accessed by a user.  Prevents idle-logoff for a time.</para>
		/// <para>Has no effect on unauthenticated or expired sessions.</para>
		/// </summary>
		public void TouchNow()
		{
			if (userName != null && !Expired)
			{
				lastTouched = SessionManager.CurrentTime;
				SessionManager.PersistSessionsToDisk();
			}
		}
		/// <summary>
		/// Call when the session completes an authentication challenge and should now be considered logged in.
		/// </summary>
		/// <param name="userName">User name which logged in.</param>
		public void UserHasAuthenticated(string userName)
		{
			this.authChallenge = null;
			this.userName = userName;
			SessionManager.PersistSessionsToDisk();
		}
	}
}

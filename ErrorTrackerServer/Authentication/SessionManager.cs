using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using BPUtil;
using Newtonsoft.Json;
using System.IO;

namespace ErrorTrackerServer
{

	public static class SessionManager
	{
		/// <summary>
		/// The maximum amount of time, in milliseconds, that a session should remain active if no requests are received using it.
		/// </summary>
		public const long SessionMaxIdleTime = 2160 * 60 * 1000; // 1.5 days
		/// <summary>
		/// The maximum amount of time, in milliseconds, that an UNAUTHENTICATED session should remain active.
		/// </summary>
		public const long SessionMaxUnauthenticatedTime = 15 * 1000; // 15 seconds

		/// <summary>
		/// Gets the current time in milliseconds since the Unix Epoch (1970/1/1 midnight UTC).
		/// </summary>
		public static long CurrentTime { get { return TimeUtil.GetTimeInMsSinceEpoch(); } }

		/// <summary>
		/// The CurrentTime value at the moment SessionManager was created.
		/// </summary>
		private static long StartTime = CurrentTime;

		/// <summary>
		/// Number of milliseconds to wait between maintenance loops.
		/// </summary>
		private const long MaintenanceInterval = 60000;

		/// <summary>
		/// The next maintenance should not occur before this time value (CurrentTime).
		/// </summary>
		private static long NextMaintenance = CurrentTime + MaintenanceInterval;

		private static ConcurrentDictionary<string, ServerSession> activeSessions = new ConcurrentDictionary<string, ServerSession>();

		static SessionManager()
		{
			LoadPersistedSessionsFromDisk();
			Logger.Info("Loaded persisted sessions from disk: " + JsonConvert.SerializeObject(activeSessions.Values.OrderBy(s => s.lastTouched), Formatting.Indented));
		}

		/// <summary>
		/// Attempts to return the specified session, if it is in the active sessions map and has not expired.  If the specified session is expired or not found, returns null.
		/// </summary>
		/// <param name="sid">The session's Session ID string.</param>
		/// <param name="touch">If false, the session won't be touched.  Pass false if you are looking at an existing session without owning it.</param>
		/// <returns></returns>
		public static ServerSession GetSession(string sid, bool touch = true)
		{
			Maintain();
			activeSessions.TryGetValue(sid, out ServerSession session);
			if (session != null && session.Expired)
			{
				RemoveSession(sid);
				session = null;
			}
			else if (session != null && touch)
				session.TouchNow();
			return session;
		}

		/// <summary>
		/// Adds a session to the active sessions map.
		/// </summary>
		/// <param name="session">Session to add.</param>
		public static void AddSession(ServerSession session)
		{
			Maintain();
			activeSessions.TryAdd(session.sid, session);
			PersistSessionsToDisk();
		}

		/// <summary>
		/// Attempts to remove and return the specified session, logging it out.  If the specified session is not found, returns null.
		/// </summary>
		/// <param name="sid">The session's Session ID string.</param>
		/// <returns></returns>
		public static ServerSession RemoveSession(string sid)
		{
			activeSessions.TryRemove(sid, out ServerSession session);
			PersistSessionsToDisk();
			return session;
		}

		/// <summary>
		/// Runs session maintenance if necessary.  It becomes necessary every 1 minute.
		/// Traditionally this sort of logic would run on a background thread or be synchronized with a lock, but I figure we don't need the overhead of that.  The maintenance process is simple and thread-safe.
		/// </summary>
		private static void Maintain()
		{
			if (CurrentTime > NextMaintenance)
			{
				NextMaintenance = CurrentTime + MaintenanceInterval;
				try
				{
					foreach (ServerSession session in activeSessions.Values)
					{
						if (session.Expired)
							RemoveSession(session.sid);
					}
				}
				catch (ThreadAbortException) { throw; }
				catch (Exception ex)
				{
					Logger.Debug(ex);
				}
			}
		}

		/// <summary>
		/// <para>Saves the current <see cref="activeSessions"/> dictionary to Sessions.json on disk so it can be loaded the next time ErrorTracker runs.</para>
		/// <para>This method is throttled such that its body will not run more than once per second.</para>
		/// </summary>
		public static Action PersistSessionsToDisk = Throttle.Create(PersistSessionsToDisk_Unthrottled, 1000, ex => Emailer.SendError(null, "SessionManager.PersistToDisk Failed", ex));
		private static object PersistSessionsToDisk_Lock = new object();
		private static string SessionsJsonFilePath => Globals.WritableDirectoryBase + "Sessions.json";
		/// <summary>
		/// <para>Saves the current <see cref="activeSessions"/> dictionary to Sessions.json on disk so it can be loaded the next time ErrorTracker runs.</para>
		/// <para>Unless the application is shutting down imminently, use <see cref="PersistSessionsToDisk"/> instead.</para>
		/// </summary>
		public static void PersistSessionsToDisk_Unthrottled()
		{
			// Do not run Maintain() here.  I think it would be a waste of time currently.
			lock (PersistSessionsToDisk_Lock)
			{
				string json = JsonConvert.SerializeObject(activeSessions.Values.OrderBy(s => s.lastTouched), Formatting.Indented);
				File.WriteAllText(SessionsJsonFilePath, json, ByteUtil.Utf8NoBOM);
			}
		}
		/// <summary>
		/// Reads the Sessions.json file and populates the <see cref="activeSessions"/> dictionary.
		/// </summary>
		private static void LoadPersistedSessionsFromDisk()
		{
			try
			{
				if (File.Exists(SessionsJsonFilePath))
				{
					string json = File.ReadAllText(SessionsJsonFilePath, ByteUtil.Utf8NoBOM);
					List<ServerSession> sessions = JsonConvert.DeserializeObject<List<ServerSession>>(json);
					foreach (ServerSession session in sessions)
						activeSessions[session.sid] = session;
				}
			}
			catch (Exception ex)
			{
				Emailer.SendError(null, "SessionManager.LoadPersistedSessionsFromDisk Failed", ex);
			}
		}
	}
}

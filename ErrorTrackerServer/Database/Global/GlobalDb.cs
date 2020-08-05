using BPUtil;
using ErrorTrackerServer.Database.Global.Model;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Database.Global
{
	public class GlobalDb : DBBase
	{
		#region Constructor / Fields
		/// <summary>
		/// Database version number useful for performing migrations. This number should only be incremented when migrations are in place to support upgrading all previously existing versions to this version.
		/// </summary>
		public const int dbVersion = 1;
		/// <summary>
		/// File name of the database.
		/// </summary>
		public readonly string DbFilename;
		private static bool initialized = false;
		private static object createMigrateLock = new object();
		private object dbTransactionLock = new object();
		/// <summary>
		/// Use within a "using" block to guarantee correct disposal.  Provides SQLite database access.  Not thread safe.  The DB connection will be lazy-loaded upon the first DB request.
		/// </summary>
		/// <param name="projectName">Project name, case insensitive. Please validate the project name before passing it in, as this class will create the database if it doesn't exist.</param>
		public GlobalDb()
		{
			DbFilename = "ErrorTrackerGlobalDB.s3db";
			conn = new Lazy<SQLiteConnection>(CreateDbConnection, false);
		}
		private SQLiteConnection CreateDbConnection()
		{
			SQLiteConnection c = new SQLiteConnection(Globals.WritableDirectoryBase + DbFilename, true);
			c.BusyTimeout = TimeSpan.FromSeconds(4);
			CreateOrMigrate(c);
			return c;
		}
		private void CreateOrMigrate(SQLiteConnection c)
		{
			if (!initialized)
			{
				lock (createMigrateLock)
				{
					if (!initialized)
					{
						initialized = true;

						c.EnableWriteAheadLogging();

						c.CreateTable<LoginRecord>();
						c.CreateTable<DbVersion>();

						PerformDbMigrations(c);
					}
				}
			}
		}
		/// <summary>
		/// Performs Db Migrations. As this occurs during lazy connection loading, predefined API methods are unavailable here.
		/// </summary>
		/// <param name="c">The db connection.</param>
		private void PerformDbMigrations(SQLiteConnection c)
		{
			// Get current version
			DbVersion version = c.Query<DbVersion>("SELECT * From DbVersion").FirstOrDefault();
			if (version == null)
			{
				// This is a new DB. It will start at the latest version!
				version = new DbVersion() { CurrentVersion = dbVersion };
				c.Insert(version);
			}
		}
		#endregion
		#region Helpers
		protected override void LockedTransaction(Action action)
		{
			lock (dbTransactionLock)
			{
				Robustify(() =>
				{
					conn.Value.RunInTransaction(action);
				});
			}
		}
		#endregion

		#region LoginRecord
		/// <summary>
		/// Adds a record of this login at the current date.
		/// </summary>
		/// <param name="userName">User name that was logged in. All lower case for effectively case-insensitive matching of user names.</param>
		/// <param name="ipAddress">IP Address that provided credentials for the login.</param>
		/// <param name="sessionId">Session ID that was assigned.</param>
		public void AddLoginRecord(string userName, string ipAddress, string sessionId)
		{
			Insert(new LoginRecord(userName.ToLower(), ipAddress, sessionId, TimeUtil.GetTimeInMsSinceEpoch()));
		}
		/// <summary>
		/// Gets a list of LoginRecord filtered by user name, ordered by date descending.
		/// </summary>
		/// <param name="userName">User Name (treated as case-insensitive)</param>
		/// <returns></returns>
		public List<LoginRecord> GetLoginRecordsByUserName(string userName)
		{
			return Query<LoginRecord>("SELECT * FROM LoginRecord WHERE UserName = ? ORDER BY Date DESC", userName.ToLower());
		}
		/// <summary>
		/// Gets a list of LoginRecord filtered by user name, ordered by date descending.
		/// </summary>
		/// <param name="userName">User Name (treated as case-insensitive)</param>
		/// <param name="startDate">Oldest allowable date to return login records for (ms since unix epoch). If startDate and endDate are both 0, these values are ignored.</param>
		/// <param name="endDate">Newest allowable date to return login records for (ms since unix epoch). If startDate and endDate are both 0, these values are ignored.</param>
		/// <returns></returns>
		public List<LoginRecord> GetLoginRecordsByUserName(string userName, long startDate, long endDate)
		{
			return Query<LoginRecord>("SELECT * FROM LoginRecord WHERE UserName = ? AND Date >= ? AND Date <= ? ORDER BY Date DESC", userName.ToLower(), startDate, endDate);
		}
		/// <summary>
		/// Gets a list of LoginRecord for the specified date range, ordered by date descending.
		/// </summary>
		/// <param name="startDate">Oldest allowable date to return login records for (ms since unix epoch). If startDate and endDate are both 0, these values are ignored.</param>
		/// <param name="endDate">Newest allowable date to return login records for (ms since unix epoch). If startDate and endDate are both 0, these values are ignored.</param>
		/// <returns></returns>
		public List<LoginRecord> GetLoginRecordsGlobal(long startDate, long endDate)
		{
			return Query<LoginRecord>("SELECT * FROM LoginRecord WHERE Date >= ? AND Date <= ? ORDER BY Date DESC", startDate, endDate);
		}
		#endregion
	}
}

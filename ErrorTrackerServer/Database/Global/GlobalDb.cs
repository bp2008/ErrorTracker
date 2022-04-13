using BPUtil;
using ErrorTrackerServer.Database.Creation;
using ErrorTrackerServer.Database.Global.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Database.Global
{
	public class GlobalDb : DBBase
	{
		#region Constructor / Fields
		private static bool initialized = false;
		private static object createMigrateLock = new object();
		private object dbTransactionLock = new object();
		/// <summary>
		/// Use within a "using" block to guarantee correct disposal.  Provides SQL database access.  Not thread safe.
		/// </summary>
		public GlobalDb()
		{
			CreateOrMigrate();
		}
		private void CreateOrMigrate()
		{
			if (!initialized)
			{
				lock (createMigrateLock)
				{
					if (!initialized)
					{
						DbCreation.CreateOrMigrateGlobalDb();

						initialized = true;
					}
				}
			}
		}
		#endregion
		#region Helpers
		protected override object GetTransactionLock()
		{
			return dbTransactionLock;
		}
		protected override string GetSchemaName()
		{
			return "ErrorTrackerGlobal";
		}
		protected override string GetConnectionString()
		{
			return DbCreation.GetConnectionString();
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
			Insert(new LoginRecord(userName.ToLower(), IPAddress.Parse(ipAddress), sessionId, TimeUtil.GetTimeInMsSinceEpoch()));
		}
		/// <summary>
		/// Gets a list of LoginRecord filtered by user name, ordered by date descending.
		/// </summary>
		/// <param name="userName">User Name (treated as case-insensitive)</param>
		/// <returns></returns>
		public List<LoginRecord> GetLoginRecordsByUserName(string userName)
		{
			return ExecuteQuery<LoginRecord>("SELECT * FROM ErrorTrackerGlobal.LoginRecord WHERE UserName = @usernamearg ORDER BY Date DESC", new { usernamearg = userName.ToLower() }).ToList();
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
			return ExecuteQuery<LoginRecord>("SELECT * FROM ErrorTrackerGlobal.LoginRecord WHERE UserName = @usernamearg AND Date >= @startdate AND Date <= @enddate ORDER BY Date DESC", new { usernamearg = userName.ToLower(), startdate = startDate, enddate = endDate }).ToList();
		}
		/// <summary>
		/// Gets a list of LoginRecord for the specified date range, ordered by date descending.
		/// </summary>
		/// <param name="startDate">Oldest allowable date to return login records for (ms since unix epoch). If startDate and endDate are both 0, these values are ignored.</param>
		/// <param name="endDate">Newest allowable date to return login records for (ms since unix epoch). If startDate and endDate are both 0, these values are ignored.</param>
		/// <returns></returns>
		public List<LoginRecord> GetLoginRecordsGlobal(long startDate, long endDate)
		{
			return ExecuteQuery<LoginRecord>("SELECT * FROM ErrorTrackerGlobal.LoginRecord WHERE Date >= @startdate AND Date <= @enddate ORDER BY Date DESC", new { startdate = startDate, enddate = endDate }).ToList();
		}
		#endregion
	}
}

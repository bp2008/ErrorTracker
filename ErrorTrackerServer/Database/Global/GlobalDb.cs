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
		/// <param name="date">If not null, overrides the current date for logging the event.</param>
		public void AddLoginRecord(string userName, string ipAddress, string sessionId, long? date = null)
		{
			Insert(new LoginRecord(userName.ToLower(), IPAddress.Parse(ipAddress), sessionId, date != null ? date.Value : TimeUtil.GetTimeInMsSinceEpoch()));
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
			if (startDate == 0 && endDate == 0)
				return ExecuteQuery<LoginRecord>("SELECT * FROM ErrorTrackerGlobal.LoginRecord WHERE UserName = @usernamearg ORDER BY Date DESC", new { usernamearg = userName.ToLower() }).ToList();
			else
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
			if (startDate == 0 && endDate == 0)
				return ExecuteQuery<LoginRecord>("SELECT * FROM ErrorTrackerGlobal.LoginRecord ORDER BY Date DESC").ToList();
			else
				return ExecuteQuery<LoginRecord>("SELECT * FROM ErrorTrackerGlobal.LoginRecord WHERE Date >= @startdate AND Date <= @enddate ORDER BY Date DESC", new { startdate = startDate, enddate = endDate }).ToList();
		}
		#endregion

		#region ApiToken
		/// <summary>
		/// Adds the API token to the database and assigns its ApiTokenId.
		/// </summary>
		/// <param name="token">Token to add.</param>
		public void AddApiToken(ApiToken token)
		{
			Insert(token);
		}
		/// <summary>
		/// Gets the API token with the specified hash, or null if there is none.  Revoked and expired tokens are returned too.
		/// </summary>
		/// <param name="tokenHash">Lower-case hex SHA-256 hash of the token.</param>
		/// <returns></returns>
		public ApiToken GetApiTokenByHash(string tokenHash)
		{
			return ExecuteQuery<ApiToken>("SELECT * FROM ErrorTrackerGlobal.ApiToken WHERE TokenHash = @hash", new { hash = tokenHash }).FirstOrDefault();
		}
		/// <summary>
		/// Gets the API token with the specified ID, or null if there is none.
		/// </summary>
		/// <param name="apiTokenId">ID of the token.</param>
		/// <returns></returns>
		public ApiToken GetApiToken(int apiTokenId)
		{
			return Query<ApiToken>("ApiTokenId", apiTokenId).FirstOrDefault();
		}
		/// <summary>
		/// Gets all API tokens belonging to the specified user, including revoked ones, newest first.
		/// </summary>
		/// <param name="userId">UserId of the user.</param>
		/// <returns></returns>
		public List<ApiToken> GetApiTokensByUser(int userId)
		{
			return ExecuteQuery<ApiToken>("SELECT * FROM ErrorTrackerGlobal.ApiToken WHERE UserId = @userid ORDER BY Created DESC", new { userid = userId });
		}
		/// <summary>
		/// Gets all API tokens, including revoked ones, newest first.
		/// </summary>
		/// <returns></returns>
		public List<ApiToken> GetAllApiTokens()
		{
			return ExecuteQuery<ApiToken>("SELECT * FROM ErrorTrackerGlobal.ApiToken ORDER BY Created DESC");
		}
		/// <summary>
		/// Revokes the API token with the specified ID, returning true if a token was revoked.
		/// </summary>
		/// <param name="apiTokenId">ID of the token.</param>
		/// <returns></returns>
		public bool RevokeApiToken(int apiTokenId)
		{
			return ExecuteNonQuery("UPDATE ErrorTrackerGlobal.ApiToken SET Revoked = true WHERE ApiTokenId = @id AND Revoked = false", new { id = apiTokenId }) > 0;
		}
		/// <summary>
		/// Revokes all API tokens belonging to the specified user, returning the number of tokens which were revoked.
		/// </summary>
		/// <param name="userId">UserId of the user.</param>
		/// <returns></returns>
		public int RevokeApiTokensForUser(int userId)
		{
			return ExecuteNonQuery("UPDATE ErrorTrackerGlobal.ApiToken SET Revoked = true WHERE UserId = @userid AND Revoked = false", new { userid = userId });
		}
		/// <summary>
		/// Sets the scopes of the API token with the specified ID, returning true if successful.
		/// </summary>
		/// <param name="apiTokenId">ID of the token.</param>
		/// <param name="scopes">Space-separated list of scopes.</param>
		/// <returns></returns>
		public bool SetApiTokenScopes(int apiTokenId, string scopes)
		{
			return ExecuteNonQuery("UPDATE ErrorTrackerGlobal.ApiToken SET Scopes = @scopes WHERE ApiTokenId = @id", new { id = apiTokenId, scopes }) > 0;
		}
		/// <summary>
		/// Records that the API token was used.
		/// </summary>
		/// <param name="apiTokenId">ID of the token.</param>
		/// <param name="date">Time of use in milliseconds since the unix epoch.</param>
		/// <param name="ipAddress">IP address of the client.</param>
		public void UpdateApiTokenLastUsed(int apiTokenId, long date, string ipAddress)
		{
			ExecuteNonQuery("UPDATE ErrorTrackerGlobal.ApiToken SET LastUsed = @date, LastUsedIP = @ip WHERE ApiTokenId = @id", new { id = apiTokenId, date, ip = ipAddress });
		}
		#endregion

		#region McpAuditRecord
		/// <summary>
		/// Adds a record to the MCP audit log.
		/// </summary>
		/// <param name="record">Record to add.</param>
		public void AddMcpAuditRecord(McpAuditRecord record)
		{
			Insert(record);
		}
		/// <summary>
		/// Gets MCP audit log records, newest first, optionally filtered.
		/// </summary>
		/// <param name="userName">If not null or empty, only records for this user (case-insensitive) are returned.</param>
		/// <param name="apiTokenId">If not null, only records for this token are returned.</param>
		/// <param name="toolName">If not null or empty, only records for this tool are returned.</param>
		/// <param name="startDate">If not null, only records at or after this time (ms since unix epoch) are returned.</param>
		/// <param name="endDate">If not null, only records at or before this time (ms since unix epoch) are returned.</param>
		/// <param name="limit">Maximum number of records to return.</param>
		/// <returns></returns>
		public List<McpAuditRecord> GetMcpAuditRecords(string userName, int? apiTokenId, string toolName, long? startDate, long? endDate, int limit)
		{
			PetaPoco.Sql sql = PetaPoco.Sql.Builder.Select("*").From("ErrorTrackerGlobal.McpAuditRecord");
			if (!string.IsNullOrWhiteSpace(userName))
				sql.Where("lower(UserName) = @0", userName.ToLower());
			if (apiTokenId != null)
				sql.Where("ApiTokenId = @0", apiTokenId.Value);
			if (!string.IsNullOrWhiteSpace(toolName))
				sql.Where("ToolName = @0", toolName);
			if (startDate != null)
				sql.Where("Date >= @0", startDate.Value);
			if (endDate != null)
				sql.Where("Date <= @0", endDate.Value);
			sql.OrderBy("Date DESC, McpAuditRecordId DESC");
			sql.Append("LIMIT @0", limit);
			return ExecuteQuery<McpAuditRecord>(sql);
		}
		/// <summary>
		/// Deletes MCP audit log records older than the specified time, returning the number of deleted records.
		/// </summary>
		/// <param name="ageCutoff">Records with a Date lower than this (ms since unix epoch) are deleted.</param>
		/// <returns></returns>
		public int DeleteMcpAuditRecordsOlderThan(long ageCutoff)
		{
			return ExecuteNonQuery("DELETE FROM ErrorTrackerGlobal.McpAuditRecord WHERE Date < @cutoff", new { cutoff = ageCutoff });
		}
		#endregion
	}
}

using BPUtil;
using ErrorTrackerServer.Database.Global;
using ErrorTrackerServer.Database.Global.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ErrorTrackerServer
{
	/// <summary>
	/// Creates and validates personal API tokens (see <see cref="ApiToken"/>).
	/// </summary>
	public static class ApiTokenManager
	{
		/// <summary>
		/// Every token starts with this prefix so that secret scanners can recognize leaked tokens.
		/// </summary>
		public const string TokenPrefix = "etk_";
		/// <summary>
		/// Number of random base62 characters following <see cref="TokenPrefix"/>.  48 base62 characters hold about 285 bits of entropy.
		/// </summary>
		private const int TokenRandomChars = 48;
		private const string base62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
		/// <summary>
		/// Maximum length of a token name.
		/// </summary>
		public const int MaxNameLength = 100;

		#region Creation
		/// <summary>
		/// Generates a new random token string.
		/// </summary>
		/// <returns></returns>
		public static string GenerateTokenString()
		{
			StringBuilder sb = new StringBuilder(TokenPrefix, TokenPrefix.Length + TokenRandomChars);
			byte[] buf = new byte[1];
			using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
			{
				while (sb.Length < TokenPrefix.Length + TokenRandomChars)
				{
					rng.GetBytes(buf);
					// Rejection sampling: 248 is the largest multiple of 62 that fits in a byte, so every character is equally likely.
					if (buf[0] < 248)
						sb.Append(base62[buf[0] % 62]);
				}
			}
			return sb.ToString();
		}
		/// <summary>
		/// Returns the lower-case hex SHA-256 hash of the token string.
		/// </summary>
		/// <param name="token">Token string.</param>
		/// <returns></returns>
		public static string HashToken(string token)
		{
			using (SHA256 sha = SHA256.Create())
				return Hex.ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(token))).ToLower();
		}
		/// <summary>
		/// Validates the arguments and creates a token for the user, returning the plaintext token string.  The plaintext is not stored anywhere and cannot be recovered later.
		/// </summary>
		/// <param name="user">The user the token will act as.</param>
		/// <param name="creatorIsAdmin">True if the person creating the token is an administrator (required for the "sensitive:read" scope).</param>
		/// <param name="name">Token name.</param>
		/// <param name="scopes">Scopes. If null or empty, the token gets "events:read" only.</param>
		/// <param name="projects">Project allow-list. If null or empty, the token may access all projects the user can access.</param>
		/// <param name="expires">Expiration date in milliseconds since the unix epoch, or null for no expiration.</param>
		/// <param name="token">(Output) The new token record.</param>
		/// <param name="errorMessage">(Output) Error message if the method returns null.</param>
		/// <returns>The plaintext token string, or null if the arguments were invalid.</returns>
		public static string CreateToken(User user, bool creatorIsAdmin, string name, string[] scopes, string[] projects, long? expires, out ApiToken token, out string errorMessage)
		{
			token = null;
			name = name?.Trim();
			if (string.IsNullOrWhiteSpace(name))
			{
				errorMessage = "The token needs a name.";
				return null;
			}
			if (name.Length > MaxNameLength)
			{
				errorMessage = "The token name may not be longer than " + MaxNameLength + " characters.";
				return null;
			}
			if (scopes == null || scopes.Length == 0)
				scopes = new string[] { ApiTokenScopes.EventsRead };
			scopes = scopes.Select(s => s?.Trim()).Distinct().ToArray();
			foreach (string scope in scopes)
			{
				if (!ApiTokenScopes.IsValid(scope))
				{
					errorMessage = "Unknown scope \"" + scope + "\".";
					return null;
				}
			}
			if (scopes.Contains(ApiTokenScopes.SensitiveRead) && !creatorIsAdmin)
			{
				errorMessage = "Only an administrator may grant the \"" + ApiTokenScopes.SensitiveRead + "\" scope.";
				return null;
			}
			List<string> projectNames = new List<string>();
			if (projects != null)
			{
				foreach (string pName in projects.Where(p => !string.IsNullOrWhiteSpace(p)))
				{
					Project p = Settings.data.GetProject(pName.Trim());
					if (p == null || !(user.IsProjectAllowed(p.Name) || user.IsProjectAllowedReadOnly(p.Name)))
					{
						errorMessage = "Project \"" + pName + "\" was not found or is not accessible by user \"" + user.Name + "\".";
						return null;
					}
					if (!projectNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
						projectNames.Add(p.Name);
				}
			}
			long now = TimeUtil.GetTimeInMsSinceEpoch();
			if (expires != null && expires.Value <= now)
			{
				errorMessage = "The expiration date must be in the future.";
				return null;
			}

			string plaintext = GenerateTokenString();
			token = new ApiToken()
			{
				UserId = user.UserId,
				UserName = user.Name,
				Name = name,
				TokenHash = HashToken(plaintext),
				TokenPrefix = plaintext.Substring(0, 8),
				Scopes = string.Join(" ", ApiTokenScopes.All.Where(s => scopes.Contains(s))),
				Projects = projectNames.Count == 0 ? null : string.Join("\n", projectNames),
				Created = now,
				Expires = expires
			};
			using (GlobalDb db = new GlobalDb())
				db.AddApiToken(token);
			Logger.Info("API token " + token.ApiTokenId + " (\"" + token.Name + "\") was created for user \"" + user.Name + "\" with scopes \"" + token.Scopes + "\"");
			errorMessage = null;
			return plaintext;
		}
		#endregion

		#region Validation
		/// <summary>
		/// The result of successful token authentication.
		/// </summary>
		public class AuthResult
		{
			/// <summary>
			/// The token which was presented.
			/// </summary>
			public ApiToken Token;
			/// <summary>
			/// The user the token acts as.
			/// </summary>
			public User User;
		}
		/// <summary>
		/// Finds the user with the specified UserId, or null.
		/// </summary>
		public static User GetUserById(int userId)
		{
			return Settings.data.GetAllUsers().FirstOrDefault(u => u.UserId == userId);
		}
		/// <summary>
		/// Returns the token's user if the token is currently usable (not revoked, not expired, and its user still exists under the same name).  Otherwise returns null.
		/// </summary>
		/// <param name="token">Token record.</param>
		/// <returns></returns>
		public static User GetUserIfTokenUsable(ApiToken token)
		{
			if (token == null || token.Revoked)
				return null;
			if (token.Expires != null && token.Expires.Value <= TimeUtil.GetTimeInMsSinceEpoch())
				return null;
			User user = GetUserById(token.UserId);
			if (user == null || !string.Equals(user.Name, token.UserName, StringComparison.OrdinalIgnoreCase))
				return null;
			return user;
		}
		/// <summary>
		/// Authenticates a token string.  Returns null if the token is missing, malformed, unknown, revoked or expired, or if its user was deleted or renamed.  Callers must not reveal which.
		/// </summary>
		/// <param name="tokenString">The token string from the "Authorization: Bearer" header.</param>
		/// <param name="ipAddress">IP address of the client, recorded as the token's last-used IP.</param>
		/// <returns></returns>
		public static AuthResult Authenticate(string tokenString, string ipAddress)
		{
			if (string.IsNullOrEmpty(tokenString) || !tokenString.StartsWith(TokenPrefix) || tokenString.Length > 200)
				return null;
			ApiToken token;
			using (GlobalDb db = new GlobalDb())
				token = db.GetApiTokenByHash(HashToken(tokenString));
			User user = GetUserIfTokenUsable(token);
			if (user == null)
				return null;
			RecordUse(token, ipAddress);
			return new AuthResult() { Token = token, User = user };
		}
		/// <summary>
		/// Maps ApiTokenId to the time when LastUsed was last written to the database.
		/// </summary>
		private static ConcurrentDictionary<int, long> lastUseWrites = new ConcurrentDictionary<int, long>();
		/// <summary>
		/// Updates the token's LastUsed date and IP, at most once a minute per token (unless the IP changes).
		/// </summary>
		private static void RecordUse(ApiToken token, string ipAddress)
		{
			long now = TimeUtil.GetTimeInMsSinceEpoch();
			lastUseWrites.TryGetValue(token.ApiTokenId, out long lastWrite);
			if (now - lastWrite < 60000 && token.LastUsedIP == ipAddress)
				return;
			lastUseWrites[token.ApiTokenId] = now;
			try
			{
				using (GlobalDb db = new GlobalDb())
					db.UpdateApiTokenLastUsed(token.ApiTokenId, now, ipAddress);
			}
			catch (Exception ex)
			{
				Logger.Debug(ex, "Failed to update API token last-used date.");
			}
		}
		#endregion

		#region Failed attempt rate limiting
		/// <summary>
		/// Maximum number of failed token attempts allowed from one IP address within <see cref="FailureWindowMs"/>.
		/// </summary>
		public const int MaxFailuresPerWindow = 10;
		/// <summary>
		/// Length of the rate-limiting window in milliseconds.
		/// </summary>
		public const long FailureWindowMs = 10 * 60 * 1000;
		private class FailureInfo
		{
			public object syncLock = new object();
			public Queue<long> failureTimes = new Queue<long>();
		}
		private static ConcurrentDictionary<string, FailureInfo> failuresByIp = new ConcurrentDictionary<string, FailureInfo>();
		/// <summary>
		/// Returns true if the IP address has had too many failed token attempts recently.  While blocked, no token is even checked, so a correct guess can't be confirmed.
		/// </summary>
		/// <param name="ipAddress">Client IP address.</param>
		/// <param name="retryAfterSeconds">(Output) How long the client should wait before trying again.</param>
		/// <returns></returns>
		public static bool IsRateLimited(string ipAddress, out int retryAfterSeconds)
		{
			retryAfterSeconds = 0;
			if (ipAddress == null || !failuresByIp.TryGetValue(ipAddress, out FailureInfo info))
				return false;
			long now = TimeUtil.GetTimeInMsSinceEpoch();
			lock (info.syncLock)
			{
				while (info.failureTimes.Count > 0 && info.failureTimes.Peek() < now - FailureWindowMs)
					info.failureTimes.Dequeue();
				if (info.failureTimes.Count < MaxFailuresPerWindow)
					return false;
				retryAfterSeconds = (int)Math.Ceiling((info.failureTimes.Peek() + FailureWindowMs - now) / 1000.0).Clamp(1, int.MaxValue);
				return true;
			}
		}
		/// <summary>
		/// Records a failed token attempt from the IP address.
		/// </summary>
		/// <param name="ipAddress">Client IP address.</param>
		public static void RecordFailure(string ipAddress)
		{
			if (ipAddress == null)
				return;
			long now = TimeUtil.GetTimeInMsSinceEpoch();
			FailureInfo info = failuresByIp.GetOrAdd(ipAddress, ip => new FailureInfo());
			lock (info.syncLock)
			{
				info.failureTimes.Enqueue(now);
				while (info.failureTimes.Count > MaxFailuresPerWindow)
					info.failureTimes.Dequeue();
			}
			if (failuresByIp.Count > 10000)
			{
				// Forget IPs with no recent failures so the dictionary can't grow without bound.
				foreach (KeyValuePair<string, FailureInfo> kvp in failuresByIp)
				{
					lock (kvp.Value.syncLock)
					{
						if (kvp.Value.failureTimes.Count == 0 || kvp.Value.failureTimes.Last() < now - FailureWindowMs)
							failuresByIp.TryRemove(kvp.Key, out FailureInfo removed);
					}
				}
			}
		}
		/// <summary>
		/// Forgets all failed token attempts.  For unit tests.
		/// </summary>
		public static void ResetRateLimits()
		{
			failuresByIp.Clear();
		}
		#endregion
	}
}

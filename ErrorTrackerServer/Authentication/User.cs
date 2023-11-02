using BPUtil;
using ErrorTrackerServer.Database.Project.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	public class User
	{
		/// <summary>
		/// Unique integer identifier for the user. This is primarily used when a database needs to refer to a User. The web admin interface keys on the Name field.
		/// </summary>
		public int UserId;
		/// <summary>
		/// Lock used when auto generating a UserId.
		/// </summary>
		private static object userIdLock = new object();
		/// <summary>
		/// The user name (unique, case-insensitive).
		/// </summary>
		public string Name;

		/// <summary>
		/// The email address of the user, to be used for notifications about failed login attempts, password recovery, etc.
		/// This is optional, not unique, and not a replacement for a user name.
		/// </summary>
		public string Email;

		/// <summary>
		/// The password, salted, hashed, and hashed again.
		/// </summary>
		public byte[] PasswordHash;

		/// <summary>
		/// A BCrypt salt value is uniquely generated for each user and using during password hashing on the client.
		/// The raw password never needs to be sent to the server.
		/// </summary>
		public string Salt;

		/// <summary>
		/// If true, this user has full administrator privilege and can manage projects.
		/// </summary>
		public bool IsAdmin = false;

		/// <summary>
		/// If true, the web administration interface cannot delete this user or change its IsAdmin flag.  A permanent user can still be renamed, because some people may prefer this for security reasons.
		/// </summary>
		public bool Permanent = false;

		/// <summary>
		/// JSON-serialized dictionary mapping project name to a custom tag key to include in event summaries loaded by this user.
		/// </summary>
		public string EventListCustomTagKeys;

		/// <summary>
		/// JSON-serialized list of PUSH notification subscriptions.
		/// This exist as a JSON-serialized string in order to help prevent concurrent access problems (e.g. when saving the settings).
		/// Each item in the list is an object containing the project name, folder Id, and subscription key string.
		/// </summary>
		public string PushSubscriptionList = null;

		/// <summary>
		/// Returns the validated tag key for the specified project, or null.
		/// </summary>
		/// <param name="projectName">Project name, not case sensitive.</param>
		/// <returns></returns>
		public string GetEventListCustomTagKey(string projectName)
		{
			projectName = projectName.ToLower();
			string json = EventListCustomTagKeys;
			if (string.IsNullOrWhiteSpace(json))
				return null;
			Dictionary<string, string> dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
			if (dict.TryGetValue(projectName, out string customTagKey))
				if (customTagKey != null)
					return Tag.ValidateTagKey(customTagKey);
			return null;
		}

		/// <summary>
		/// Sets the validated tag key for the specified project.
		/// </summary>
		/// <param name="projectName">Project name, not case sensitive.</param>
		/// <param name="customTagKey">Tag key to set. If null, the record is deleted.</param>
		/// <returns></returns>
		public void SetEventListCustomTagKey(string projectName, string customTagKey)
		{
			projectName = projectName.ToLower();
			string json = EventListCustomTagKeys;
			Dictionary<string, string> dict;
			if (string.IsNullOrWhiteSpace(json))
				dict = new Dictionary<string, string>();
			else
			{
				try
				{
					dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
				}
				catch
				{
					dict = new Dictionary<string, string>();
				}
			}
			if (customTagKey == null && dict.ContainsKey(projectName))
				dict.Remove(projectName);
			else
				dict[projectName] = Tag.ValidateTagKey(customTagKey);
			EventListCustomTagKeys = JsonConvert.SerializeObject(dict);
		}

		/// <summary>
		/// Returns an array of <see cref="PushSubscription.subscriptionKey"/> if the user is subscribed to new events in the specified folder.  If the user is not subscribed to this folder, returns an empty array. Each subscription key is unique to a web browser instance.
		/// </summary>
		/// <param name="projectName">Project name containing the folder.</param>
		/// <param name="folderId">ID of the folder we're checking subscription status for.</param>
		/// <returns></returns>
		public string[] GetPushNotificationSubscriptions(string projectName, int folderId)
		{
			string json = PushSubscriptionList;
			if (!string.IsNullOrWhiteSpace(json))
			{
				PushSubscription[] subs = JsonConvert.DeserializeObject<PushSubscription[]>(json);
				projectName = projectName.ToLower();
				return subs
					.Where(s => s.projectName == projectName && s.folderId == folderId)
					.Select(s => s.subscriptionKey).ToArray();
			}
			return new string[0];
		}

		/// <summary>
		/// Subscribes or unsubscribes the user to PUSH notifications for new events in the specified folder.
		/// </summary>
		/// <param name="projectName">Project name containing the folder.</param>
		/// <param name="folderId">ID of the folder we're setting subscription status for.</param>
		/// <param name="subscriptionKey">Subscription key (unique to each web browser instance)</param>
		/// <param name="subscribed">If true, the user will be subscribed. If false, unsubscribed.</param>
		/// <returns></returns>
		public void SetPushNotificationSubscription(string projectName, int folderId, string subscriptionKey, bool subscribed)
		{
			List<PushSubscription> subs = null;

			string json = PushSubscriptionList;
			if (!string.IsNullOrWhiteSpace(json))
				subs = JsonConvert.DeserializeObject<List<PushSubscription>>(json);

			if (subs == null)
				subs = new List<PushSubscription>();

			projectName = projectName.ToLower();

			bool changed = false;
			for (int i = 0; i < subs.Count; i++)
			{
				PushSubscription sub = subs[i];
				if (sub.projectName == projectName && sub.folderId == folderId && sub.subscriptionKey == subscriptionKey)
				{
					if (subscribed)
						return;
					else
					{
						changed = true;
						subs.RemoveAt(i);
						i--;
					}
				}
			}

			if (subscribed)
			{
				changed = true;
				subs.Add(new PushSubscription(projectName, folderId, subscriptionKey));
			}
			if (changed)
				PushSubscriptionList = JsonConvert.SerializeObject(subs);
		}
		/// <summary>
		/// Removes all subscriptions using this key, returning the number of removed subscription objects.
		/// </summary>
		/// <param name="subscriptionKey">Subscription key (unique to each web browser instance)</param>
		public int DeletePushNotificationSubscriptions(string subscriptionKey)
		{
			List<PushSubscription> subs = null;
			string json = PushSubscriptionList;
			if (!string.IsNullOrWhiteSpace(json))
				subs = JsonConvert.DeserializeObject<List<PushSubscription>>(json);

			if (subs == null)
				subs = new List<PushSubscription>();

			int count = 0;

			for (int i = 0; i < subs.Count; i++)
			{
				PushSubscription sub = subs[i];
				if (sub.subscriptionKey == subscriptionKey)
				{
					count++;
					subs.RemoveAt(i);
					i--;
				}
			}

			if (count > 0)
				PushSubscriptionList = JsonConvert.SerializeObject(subs);

			return count;
		}

		/// <summary>
		/// Deletes this user's push notification subscriptions for the specified folder, returning true if any were deleted.
		/// </summary>
		/// <param name="projectName">Project name containing the folder.</param>
		/// <param name="folderId">ID of the folder we're deleting subscriptions for.</param>
		/// <returns></returns>
		public bool DeletePushNotificationSubscriptionsByFolder(string projectName, int folderId)
		{
			List<PushSubscription> subs = null;

			string json = PushSubscriptionList;
			if (!string.IsNullOrWhiteSpace(json))
				subs = JsonConvert.DeserializeObject<List<PushSubscription>>(json);

			if (subs == null)
				subs = new List<PushSubscription>();

			projectName = projectName.ToLower();

			bool changed = false;
			for (int i = 0; i < subs.Count; i++)
			{
				PushSubscription sub = subs[i];
				if (sub.projectName == projectName && sub.folderId == folderId)
				{
					changed = true;
					subs.RemoveAt(i);
					i--;
				}
			}

			if (changed)
				PushSubscriptionList = JsonConvert.SerializeObject(subs);

			return changed;
		}

		public void ClearAllPushNotificationSubscriptions()
		{
			PushSubscriptionList = null;
		}

		/// <summary>
		/// Don't query this directly -- use instance methods like <see cref="AllowProject"/> and <see cref="IsProjectAllowed"/> instead to guarantee thread safety.
		/// (List of users)
		/// </summary>
		public List<string> Internal_AllowedProjects = new List<string>();
		private ReaderWriterLockSlim allowedProjectsLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
		/// <summary>
		/// Don't query this directly -- use instance methods like <see cref="AllowReadOnlyProject"/> and <see cref="IsProjectAllowed"/> instead to guarantee thread safety.
		/// (List of users)
		/// </summary>
		public List<string> Internal_ReadOnlyProjects = new List<string>();
		private ReaderWriterLockSlim readOnlyProjectsLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

		public User()
		{
		}

		/// <summary>
		/// Creates a new user and generates its Salt value.
		/// </summary>
		/// <param name="Name"></param>
		/// <param name="Password"></param>
		/// <param name="DisplayName"></param>
		/// <param name="Email"></param>
		/// <param name="IsAdmin"></param>
		public User(string Name, string Password, string Email, bool IsAdmin)
		{
			this.Name = Name;
			this.Email = Email;
			this.Salt = Util.BCryptSalt();
			this.PasswordHash = HashPassword(Password);
			this.IsAdmin = IsAdmin;
		}

		/// <summary>
		/// Sets the password for this user.
		/// </summary>
		/// <param name="password">The plain text password to set for this user.</param>
		public void SetPassword(string password)
		{
			if (this.Salt == null)
				this.Salt = Util.BCryptSalt();
			this.PasswordHash = HashPassword(password);
		}
		private byte[] HashPassword(string password)
		{
			// Append the salt as a hex formatted string instead of as raw data.
			// This makes it easier to deal with in JavaScript.
			byte[] onceHashedPw = Hash.GetSHA512Bytes(Encoding.UTF8.GetBytes(Util.BCryptHash(password, Salt)));
			byte[] passwordHash = Hash.GetSHA512Bytes(onceHashedPw);
			return passwordHash;
		}

		/// <summary>
		/// Authentication protocol based on: 
		/// http://openwall.info/wiki/people/solar/algorithms/challenge-response-authentication
		/// 
		/// This authentication protocol is no replacement for proper HTTPS with an SSL certificate, 
		/// and exists mostly to protect user passwords in the event that they are used with an 
		/// unsecure connection.
		/// 
		/// IMPORTANT NOTES: 
		///		1) An attacker sniffing unencryted traffic will be able to hijack active sessions 
		///			quite easily.
		///		2) Setting a password (when creating or updating an account) is not 
		///			cryptographically secured unless you use HTTPS.  If an attacker sniffs this 
		///			traffic, the account is compromised.  Further, the password will be 
		///			discoverable by a relatively easy brute force attack.
		///		3) This authentication protocol grants protection against replay attacks.  An 
		///			attacker sniffing the login protocol will not be able to authenticate new 
		///			sessions, and should not be able to discover the hashed password.
		///		4) Passwords are always salted and hashed before being transmitted, making them 
		///			resistant to attacks with rainbow tables.
		/// 
		/// The way this works is, here on the server we only store the password after it has been 
		/// salted and hashed by two iterations of SHA512. This protects user passwords in the 
		/// event that the server's user database is stolen.
		/// The client can verify its identity by sending us the password after it has been salted 
		/// and hashed only one time, and we can authenticate the user by hashing a second time on 
		/// the server.
		/// 
		/// The trick is, the client needs to transmit the once-hashed password in such a way that 
		/// the server can read it, but someone intercepting traffic can not.
		/// To achieve this, we build a token "challengeHashed" which can be easily generated by 
		/// both the client and server, but not by an attacker intercepting traffic.  The client 
		/// XORs the once-hashed password with the challengeHashed token, and transmits this result.
		/// An attacker intercepting traffic can only see the result of XORing these two tokens, and 
		/// cannot reconstruct the original tokens.  However on the server we know the value of the 
		/// challengeHashed token so we can reverse the XOR operation to obtain the once-hashed
		/// password.  Essentially, challengeHashed is a single-use encryption key.
		/// 
		/// This method returns true if the response is validated and the user is authenticated.
		/// </summary>
		/// <param name="response">A response from the user (hex).</param>
		/// <param name="challenge">The challenge token from which the user's response was created.</param>
		/// <returns>true if the user is authenticated successfully</returns>
		public bool AuthenticateUser(string response, byte[] challenge)
		{
			byte[] responseBytes = Hex.ToByteArray(response);
			byte[] challengeHashed = Hash.GetSHA512Bytes(PasswordHash, challenge);
			byte[] onceHashedPw = ByteUtil.XORByteArrays(challengeHashed, responseBytes);
			byte[] hashedAgain = Hash.GetSHA512Bytes(onceHashedPw);
			bool authenticationSuccess = Util.ArraysEqual(hashedAgain, PasswordHash);
			return authenticationSuccess;
		}

		/// <summary>
		/// Adds the project to the user's list of allowed projects (does nothing if the project is already in the list).  Project names are case-insensitive.
		/// </summary>
		/// <param name="projectName">Project name (case-insensitive).</param>
		/// <returns></returns>
		public void AllowProject(string projectName)
		{
			Settings._TryAddListItem(projectName, Internal_AllowedProjects, s => s, allowedProjectsLock);
		}
		/// <summary>
		/// Removes the project from the user's list of allowed projects, returning true if the project was removed.  Returns false if the project was not in the list.
		/// </summary>
		/// <param name="projectName">Project name (case-insensitive).</param>
		/// <returns></returns>
		public bool DisallowProject(string projectName)
		{
			return Settings._TryRemoveListItem(projectName, Internal_AllowedProjects, s => s, allowedProjectsLock) != null;
		}
		/// <summary>
		/// Returns true if the specified project name is in this user's list of allowed projects.
		/// </summary>
		/// <param name="projectName">Project name (case-insensitive).</param>
		/// <returns></returns>
		public bool IsProjectAllowed(string projectName)
		{
			return Settings._GetListItem(projectName, Internal_AllowedProjects, s => s, allowedProjectsLock) != null;
		}
		/// <summary>
		/// Returns a snapshot of this user's list of allowed projects.
		/// </summary>
		/// <returns></returns>
		public List<string> GetAllowedProjects()
		{
			return Settings._GetList(Internal_AllowedProjects, allowedProjectsLock);
		}
		/// <summary>
		/// Returns true if the specified project name is in this user's list of allowed projects.
		/// </summary>
		/// <param name="projectName">Project name (case-insensitive).</param>
		/// <returns></returns>
		public void SetAllowedProjects(List<string> newAllowedProjects)
		{
			Settings._UpdateList(newAllowedProjects, Internal_AllowedProjects, allowedProjectsLock);
		}
		/// <summary>
		/// Assigns a unique UserId, only if one is not already set. Returns true if the UserId was set by the method call.
		/// </summary>
		public bool InitializeUserId()
		{
			if (UserId == 0)
			{
				lock (userIdLock)
				{
					if (UserId == 0)
					{
						int[] allUserIds = Settings.data.GetAllUsers().Select(u => u.UserId).ToArray();
						int max = 0;
						if (allUserIds.Length > 0)
							max = Math.Max(0, allUserIds.Max());
						UserId = max + 1;
						return true;
					}
				}
			}
			return false;
		}
	}
}

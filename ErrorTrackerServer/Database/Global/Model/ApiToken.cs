using System;
using System.Collections.Generic;
using System.Linq;

namespace ErrorTrackerServer.Database.Global.Model
{
	/// <summary>
	/// A personal API token.  Only a SHA-256 hash of the token is stored; the token itself is shown to its owner once, when it is created.
	/// </summary>
	public class ApiToken
	{
		/// <summary>
		/// Auto-incremented unique identifier for the token.
		/// </summary>
		public int ApiTokenId { get; set; }
		/// <summary>
		/// <see cref="User.UserId"/> of the user the token acts as.
		/// </summary>
		public int UserId { get; set; }
		/// <summary>
		/// Name of the user at the time the token was created.  The token stops working if the user is renamed, so that a token can never act as a different user who later receives the same name or ID.
		/// </summary>
		public string UserName { get; set; }
		/// <summary>
		/// A name for the token, chosen by its owner.
		/// </summary>
		public string Name { get; set; }
		/// <summary>
		/// Lower-case hex SHA-256 hash of the token.
		/// </summary>
		public string TokenHash { get; set; }
		/// <summary>
		/// The first 8 characters of the token, so that people can tell their tokens apart.
		/// </summary>
		public string TokenPrefix { get; set; }
		/// <summary>
		/// Space-separated list of scopes (see <see cref="ApiTokenScopes"/>).
		/// </summary>
		public string Scopes { get; set; }
		/// <summary>
		/// Newline-separated list of project names the token is restricted to.  Null or empty means all projects the user can access.
		/// </summary>
		public string Projects { get; set; }
		/// <summary>
		/// Creation date in milliseconds since the unix epoch.
		/// </summary>
		public long Created { get; set; }
		/// <summary>
		/// Expiration date in milliseconds since the unix epoch, or null if the token does not expire.
		/// </summary>
		public long? Expires { get; set; }
		/// <summary>
		/// The last time the token was used, in milliseconds since the unix epoch.  Updated at most once per minute.
		/// </summary>
		public long? LastUsed { get; set; }
		/// <summary>
		/// The IP address which last used the token.
		/// </summary>
		public string LastUsedIP { get; set; }
		/// <summary>
		/// True if the token has been revoked.
		/// </summary>
		public bool Revoked { get; set; }

		/// <summary>
		/// Returns the token's scopes as an array.
		/// </summary>
		public string[] GetScopes()
		{
			if (string.IsNullOrWhiteSpace(Scopes))
				return new string[0];
			return Scopes.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		}
		/// <summary>
		/// Returns true if the token has the specified scope.
		/// </summary>
		public bool HasScope(string scope)
		{
			return GetScopes().Contains(scope);
		}
		/// <summary>
		/// Returns the project allow-list as an array, or null if the token is not restricted to specific projects.
		/// </summary>
		public string[] GetProjects()
		{
			if (string.IsNullOrWhiteSpace(Projects))
				return null;
			return Projects.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
		}
		/// <summary>
		/// Returns true if the token's project allow-list includes the project (case-insensitive).
		/// </summary>
		public bool AllowsProject(string projectName)
		{
			string[] projects = GetProjects();
			if (projects == null)
				return true;
			return projects.Any(p => p.Equals(projectName, StringComparison.OrdinalIgnoreCase));
		}
	}
	/// <summary>
	/// The scopes an API token may have.
	/// </summary>
	public static class ApiTokenScopes
	{
		/// <summary>
		/// Projects, folders, event search, event details, statistics, filter list/get/preview.  Requires read access to the project.
		/// </summary>
		public const string EventsRead = "events:read";
		/// <summary>
		/// Move events and set their color.  Requires write access to the project.  (Setting the token user's own read state only needs <see cref="EventsRead"/>.)
		/// </summary>
		public const string EventsWrite = "events:write";
		/// <summary>
		/// Create, update, delete and reorder filters.  Requires write access to the project.
		/// </summary>
		public const string FiltersWrite = "filters:write";
		/// <summary>
		/// Run filters against existing events.  Requires write access to the project.
		/// </summary>
		public const string FiltersRun = "filters:run";
		/// <summary>
		/// Bypass redaction of MCP output.  Only an admin may grant this.
		/// </summary>
		public const string SensitiveRead = "sensitive:read";
		/// <summary>
		/// All scopes, in display order.
		/// </summary>
		public static readonly string[] All = new string[] { EventsRead, EventsWrite, FiltersWrite, FiltersRun, SensitiveRead };
		/// <summary>
		/// Returns true if the given string is a known scope.
		/// </summary>
		public static bool IsValid(string scope)
		{
			return All.Contains(scope);
		}
		/// <summary>
		/// Returns true if the scope requires write access to the project.
		/// </summary>
		public static bool RequiresWriteAccess(string scope)
		{
			return scope == EventsWrite || scope == FiltersWrite || scope == FiltersRun;
		}
	}
}

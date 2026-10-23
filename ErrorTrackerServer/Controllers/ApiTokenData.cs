using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Database.Global;
using ErrorTrackerServer.Database.Global.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ErrorTrackerServer.Controllers
{
	/// <summary>
	/// Lets users manage their own personal API tokens (used by the MCP endpoint).
	/// </summary>
	public class ApiTokenData : UserController
	{
		/// <summary>
		/// Returns whether MCP is enabled, plus the user's tokens and what they may grant.
		/// </summary>
		public ActionResult GetMyTokens()
		{
			User user = session.GetUser();
			GetMyTokensResponse response = new GetMyTokensResponse();
			response.mcpEnabled = Settings.data.mcpEnabled;
			response.isAdmin = user.IsAdmin;
			response.scopes = ApiTokenScopes.All.Where(s => s != ApiTokenScopes.SensitiveRead || user.IsAdmin).ToArray();
			response.projects = Settings.data.GetAllProjects()
				.Where(p => user.IsProjectAllowed(p.Name) || user.IsProjectAllowedReadOnly(p.Name))
				.Select(p => p.Name)
				.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
				.ToArray();
			response.mcpUrl = GetMcpUrl();
			if (Settings.data.mcpEnabled)
			{
				using (GlobalDb db = new GlobalDb())
					response.tokens = db.GetApiTokensByUser(user.UserId).Select(t => new ApiTokenInfo(t)).ToList();
			}
			else
				response.tokens = new List<ApiTokenInfo>();
			return Json(response);
		}
		/// <summary>
		/// Creates a token for the current user and returns the plaintext token (shown once).
		/// </summary>
		public ActionResult CreateToken()
		{
			CreateTokenRequest request = ApiRequestBase.ParseRequest<CreateTokenRequest>(this);
			if (!Settings.data.mcpEnabled)
				return ApiError("API tokens are not available because MCP is disabled. An administrator can enable it in Settings.");
			User user = session.GetUser();
			string plaintext = ApiTokenManager.CreateToken(user, user.IsAdmin, request.name, request.scopes, request.projects, request.expires, out ApiToken token, out string errorMessage);
			if (plaintext == null)
				return ApiError(errorMessage);
			CreateTokenResponse response = new CreateTokenResponse();
			response.token = plaintext;
			response.info = new ApiTokenInfo(token);
			response.mcpUrl = GetMcpUrl();
			return Json(response);
		}
		/// <summary>
		/// Revokes one of the current user's tokens.
		/// </summary>
		public ActionResult RevokeToken()
		{
			OneTokenRequest request = ApiRequestBase.ParseRequest<OneTokenRequest>(this);
			User user = session.GetUser();
			using (GlobalDb db = new GlobalDb())
			{
				ApiToken token = db.GetApiToken(request.apiTokenId);
				if (token == null || token.UserId != user.UserId)
					return ApiError("Token " + request.apiTokenId + " was not found.");
				db.RevokeApiToken(token.ApiTokenId);
			}
			Logger.Info("API token " + request.apiTokenId + " was revoked by its owner \"" + user.Name + "\"");
			return Json(new ApiResponseBase(true));
		}
		/// <summary>
		/// Returns the absolute URL of the MCP endpoint, based on the public base URL setting or, failing that, the request.
		/// </summary>
		private string GetMcpUrl()
		{
			string baseUrl = global::ErrorTrackerServer.Mcp.McpOutput.GetPublicBaseUrl();
			if (baseUrl == null)
			{
				Uri url = Context.httpProcessor.Request.Url;
				baseUrl = url.Scheme + "://" + url.Authority + Settings.data.GetAppPath();
			}
			return baseUrl + "mcp";
		}
	}
	/// <summary>
	/// A token as shown in the web interface.  Never includes the token itself or its hash.
	/// </summary>
	public class ApiTokenInfo
	{
		public int apiTokenId;
		public string userName;
		public string name;
		public string prefix;
		public string[] scopes;
		public string[] projects;
		public long created;
		public long? expires;
		public long? lastUsed;
		public string lastUsedIP;
		public bool revoked;
		/// <summary>
		/// True if the token can currently be used (not revoked or expired, and its user still exists under the same name).
		/// </summary>
		public bool active;
		public ApiTokenInfo() { }
		public ApiTokenInfo(ApiToken t)
		{
			apiTokenId = t.ApiTokenId;
			userName = t.UserName;
			name = t.Name;
			prefix = t.TokenPrefix;
			scopes = t.GetScopes();
			projects = t.GetProjects();
			created = t.Created;
			expires = t.Expires;
			lastUsed = t.LastUsed;
			lastUsedIP = t.LastUsedIP;
			revoked = t.Revoked;
			active = ApiTokenManager.GetUserIfTokenUsable(t) != null;
		}
	}
	public class GetMyTokensResponse : ApiResponseBase
	{
		public bool mcpEnabled;
		public bool isAdmin;
		public string mcpUrl;
		public string[] scopes;
		public string[] projects;
		public List<ApiTokenInfo> tokens;
		public GetMyTokensResponse() : base(true, null) { }
	}
	public class CreateTokenRequest : ApiRequestBase
	{
		public string name;
		public string[] scopes;
		public string[] projects;
		/// <summary>
		/// Expiration date in milliseconds since the unix epoch, or null.
		/// </summary>
		public long? expires;
	}
	public class CreateTokenResponse : ApiResponseBase
	{
		/// <summary>
		/// The plaintext token.  This is the only time it is ever sent.
		/// </summary>
		public string token;
		public ApiTokenInfo info;
		public string mcpUrl;
		public CreateTokenResponse() : base(true, null) { }
	}
	public class OneTokenRequest : ApiRequestBase
	{
		public int apiTokenId;
	}
}

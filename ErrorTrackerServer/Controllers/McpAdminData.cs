using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Database.Global;
using ErrorTrackerServer.Database.Global.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using McpRedactor = global::ErrorTrackerServer.Mcp.McpRedactor;

namespace ErrorTrackerServer.Controllers
{
	/// <summary>
	/// Administration of the MCP endpoint: all API tokens, redaction policy, and the audit log.
	/// </summary>
	public class McpAdminData : AdminController
	{
		#region Tokens
		public ActionResult GetAllTokens()
		{
			GetAllTokensResponse response = new GetAllTokensResponse();
			using (GlobalDb db = new GlobalDb())
				response.tokens = db.GetAllApiTokens().Select(t => new ApiTokenInfo(t)).ToList();
			return Json(response);
		}
		public ActionResult RevokeToken()
		{
			OneTokenRequest request = ApiRequestBase.ParseRequest<OneTokenRequest>(this);
			using (GlobalDb db = new GlobalDb())
			{
				if (db.GetApiToken(request.apiTokenId) == null)
					return ApiError("Token " + request.apiTokenId + " was not found.");
				db.RevokeApiToken(request.apiTokenId);
			}
			Logger.Info("API token " + request.apiTokenId + " was revoked by administrator \"" + session.userName + "\"");
			return Json(new ApiResponseBase(true));
		}
		/// <summary>
		/// Grants or removes the "sensitive:read" scope, which bypasses redaction.
		/// </summary>
		public ActionResult SetSensitiveRead()
		{
			SetSensitiveReadRequest request = ApiRequestBase.ParseRequest<SetSensitiveReadRequest>(this);
			using (GlobalDb db = new GlobalDb())
			{
				ApiToken token = db.GetApiToken(request.apiTokenId);
				if (token == null)
					return ApiError("Token " + request.apiTokenId + " was not found.");
				List<string> scopes = token.GetScopes().Where(s => s != ApiTokenScopes.SensitiveRead).ToList();
				if (request.grant)
					scopes.Add(ApiTokenScopes.SensitiveRead);
				db.SetApiTokenScopes(token.ApiTokenId, string.Join(" ", ApiTokenScopes.All.Where(s => scopes.Contains(s))));
			}
			Logger.Info("Administrator \"" + session.userName + "\" " + (request.grant ? "granted" : "removed") + " scope \"" + ApiTokenScopes.SensitiveRead + "\" " + (request.grant ? "to" : "from") + " API token " + request.apiTokenId);
			return Json(new ApiResponseBase(true));
		}
		#endregion

		#region Redaction
		public ActionResult GetRedaction()
		{
			GetRedactionResponse response = new GetRedactionResponse();
			response.hiddenTagKeys = Settings.data.mcpHiddenTagKeys ?? "";
			response.rules = (Settings.data.mcpRedactionRules ?? new McpRedactionRule[0]).Where(r => r != null).ToArray();
			response.defaultRules = McpRedactionRule.GetDefaults();
			return Json(response);
		}
		public ActionResult SetRedaction()
		{
			SetRedactionRequest request = ApiRequestBase.ParseRequest<SetRedactionRequest>(this);
			McpRedactionRule[] rules = request.rules ?? new McpRedactionRule[0];
			string error = ValidateRules(rules);
			if (error != null)
				return ApiError(error);
			Settings.data.mcpHiddenTagKeys = request.hiddenTagKeys ?? "";
			Settings.data.mcpRedactionRules = rules;
			Settings.data.Save();
			Logger.Info("MCP redaction policy was changed by \"" + session.userName + "\"");
			return Json(new ApiResponseBase(true));
		}
		/// <summary>
		/// Applies a redaction policy (not necessarily saved) to sample text, so administrators can test rules.
		/// </summary>
		public ActionResult TestRedaction()
		{
			TestRedactionRequest request = ApiRequestBase.ParseRequest<TestRedactionRequest>(this);
			McpRedactionRule[] rules = request.rules ?? new McpRedactionRule[0];
			string error = ValidateRules(rules);
			if (error != null)
				return ApiError(error);
			McpRedactor redactor = new McpRedactor(request.hiddenTagKeys, rules);
			TestRedactionResponse response = new TestRedactionResponse();
			if (!string.IsNullOrWhiteSpace(request.tagKey) && redactor.IsHiddenTag(request.tagKey))
			{
				response.hidden = true;
				response.output = "";
			}
			else if (!string.IsNullOrWhiteSpace(request.tagKey))
				response.output = redactor.RedactTag(request.tagKey, request.text ?? "");
			else
				response.output = redactor.RedactText(request.text ?? "");
			return Json(response);
		}
		private static string ValidateRules(McpRedactionRule[] rules)
		{
			for (int i = 0; i < rules.Length; i++)
			{
				McpRedactionRule r = rules[i];
				if (r == null)
					return "Rule " + (i + 1) + " is empty.";
				if (string.IsNullOrWhiteSpace(r.Pattern))
					return "Rule " + (i + 1) + " (\"" + r.Name + "\") has no pattern.";
				try
				{
					new Regex(r.Pattern, RegexOptions.CultureInvariant, McpRedactor.RuleTimeout);
				}
				catch (ArgumentException ex)
				{
					return "Rule " + (i + 1) + " (\"" + r.Name + "\") has an invalid regular expression: " + ex.Message;
				}
			}
			return null;
		}
		#endregion

		#region Audit Log
		public ActionResult GetAuditLog()
		{
			GetAuditLogRequest request = ApiRequestBase.ParseRequest<GetAuditLogRequest>(this);
			GetAuditLogResponse response = new GetAuditLogResponse();
			int limit = request.limit <= 0 ? 500 : Math.Min(request.limit, 5000);
			using (GlobalDb db = new GlobalDb())
				response.records = db.GetMcpAuditRecords(request.userName, request.apiTokenId, request.toolName, request.startDate, request.endDate, limit);
			return Json(response);
		}
		#endregion
	}
	public class GetAllTokensResponse : ApiResponseBase
	{
		public List<ApiTokenInfo> tokens;
		public GetAllTokensResponse() : base(true, null) { }
	}
	public class SetSensitiveReadRequest : ApiRequestBase
	{
		public int apiTokenId;
		public bool grant;
	}
	public class GetRedactionResponse : ApiResponseBase
	{
		public string hiddenTagKeys;
		public McpRedactionRule[] rules;
		public McpRedactionRule[] defaultRules;
		public GetRedactionResponse() : base(true, null) { }
	}
	public class SetRedactionRequest : ApiRequestBase
	{
		public string hiddenTagKeys;
		public McpRedactionRule[] rules;
	}
	public class TestRedactionRequest : SetRedactionRequest
	{
		public string text;
		/// <summary>
		/// If set, the text is treated as the value of this tag.  Otherwise it is treated as a message.
		/// </summary>
		public string tagKey;
	}
	public class TestRedactionResponse : ApiResponseBase
	{
		public string output;
		/// <summary>
		/// True if the tag would be omitted entirely.
		/// </summary>
		public bool hidden;
		public TestRedactionResponse() : base(true, null) { }
	}
	public class GetAuditLogRequest : ApiRequestBase
	{
		public string userName;
		public int? apiTokenId;
		public string toolName;
		public long? startDate;
		public long? endDate;
		public int limit;
	}
	public class GetAuditLogResponse : ApiResponseBase
	{
		public List<McpAuditRecord> records;
		public GetAuditLogResponse() : base(true, null) { }
	}
}

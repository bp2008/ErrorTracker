using BPUtil;
using ErrorTrackerServer;
using ErrorTrackerServer.Database.Creation;
using ErrorTrackerServer.Database.Global;
using ErrorTrackerServer.Database.Global.Model;
using ErrorTrackerServer.Database.Project.Model;
using ErrorTrackerServer.Filtering;
using ErrorTrackerServer.Mcp;
using ErrorTrackerServer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ErrorTrackerTests
{
	/// <summary>
	/// Tests the MCP endpoint (spec ErrorTracker-MCP-Spec.md, section 14.1).  These tests need the PostgreSQL test database (see <see cref="DbTestConfiguration"/>).
	/// </summary>
	[TestClass]
	public class TestMcp
	{
		private static User admin;
		private static User reader;
		private static string adminToken;
		private static string readerToken;
		private static string writeToken;
		private static int projectCounter = 0;
		private const string MainProject = "McpMain";
		private const string OtherProject = "McpOther";

		#region Setup
		[ClassInitialize]
		public static void ClassInit(TestContext context)
		{
			DbTestConfiguration cfg = DbTestConfiguration.Load();
			DbCreation.TEST_ONLY_CreateTestingDatabase(cfg.dbAdminUser, cfg.dbAdminPass, cfg.existingDbName);
			Settings.data.mcpEnabled = true;
			Settings.data.publicBaseUrl = "https://errors.example.com/";

			admin = new User("mcpadmin", "pw", null, true);
			reader = new User("mcpreader", "pw", null, false);
			Settings.data.TryAddUser(admin);
			admin.InitializeUserId();
			Settings.data.TryAddUser(reader);
			reader.InitializeUserId();

			CreateProject(MainProject);
			CreateProject(OtherProject);
			reader.SetAllowedProjects(new List<string>());
			reader.SetAllowedProjectsReadOnly(new List<string>() { MainProject });

			adminToken = NewToken(admin, "admin all", ApiTokenScopes.All);
			readerToken = NewToken(reader, "reader", null);
			writeToken = NewToken(admin, "writer", new string[] { ApiTokenScopes.EventsRead, ApiTokenScopes.FiltersWrite, ApiTokenScopes.FiltersRun });

			using (DB db = new DB(MainProject))
			{
				long now = TimeUtil.GetTimeInMsSinceEpoch();
				db.AddEvent(MakeEvent(EventType.Error, "NullReferenceException", "Object reference not set to an instance of an object.", now - 3600000, new Dictionary<string, string>() { ["Session ID"] = "sess-123", ["URL"] = "https://app.example.com/x" }));
				db.AddEvent(MakeEvent(EventType.Info, "Login Rate Limit", "User exceeded the login rate limit", now - 2 * 3600000, new Dictionary<string, string>() { ["IP"] = "10.0.0.1" }));
				db.AddEvent(MakeEvent(EventType.Info, "Login Rate Limit", "User exceeded the login rate limit", now - 3 * 3600000, new Dictionary<string, string>() { ["IP"] = "10.0.0.2" }));
				db.AddEvent(MakeEvent(EventType.Error, "Old", "An old error", now - 60L * 86400000, null));
			}
		}
		[TestInitialize]
		public void TestInit()
		{
			Settings.data.mcpEnabled = true;
			Settings.data.mcpAllowedOrigins = new string[0];
			Settings.data.mcpHiddenTagKeys = "";
			Settings.data.mcpRedactionRules = McpRedactionRule.GetDefaults();
			ApiTokenManager.ResetRateLimits();
		}
		private static void CreateProject(string name)
		{
			Project p = new Project() { Name = name };
			p.InitializeSubmitKey();
			Settings.data.TryAddProject(p);
			admin.AllowProject(name);
			using (DB db = new DB(name)) { }
		}
		/// <summary>
		/// Creates a new, empty project that only the admin can access.
		/// </summary>
		private static string NewProject()
		{
			string name = "McpP" + Interlocked.Increment(ref projectCounter);
			CreateProject(name);
			return name;
		}
		private static string NewToken(User user, string name, string[] scopes, string[] projects = null, long? expires = null)
		{
			string token = ApiTokenManager.CreateToken(user, user.IsAdmin, name, scopes, projects, expires, out ApiToken record, out string error);
			Assert.IsNotNull(token, error);
			return token;
		}
		private static Event MakeEvent(EventType type, string subType, string message, long date, Dictionary<string, string> tags, bool html = false)
		{
			Event e = new Event() { EventType = type, SubType = subType, Message = message, Date = date, MessageHtml = html };
			if (tags != null)
				foreach (KeyValuePair<string, string> t in tags)
					e.SetTag(t.Key, t.Value);
			return e;
		}
		#endregion

		#region HTTP helpers
		/// <summary>
		/// Parses JSON without turning ISO 8601 strings into DateTime values (which JObject.Parse does by default).
		/// </summary>
		private static JObject ParseJson(string json)
		{
			return JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings() { DateParseHandling = DateParseHandling.None });
		}
		private static McpHttpResponse Post(string token, object body, Dictionary<string, string> headers = null, string httpMethod = "POST", bool loopback = true)
		{
			Dictionary<string, string> h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (token != null)
				h["Authorization"] = "Bearer " + token;
			if (headers != null)
				foreach (KeyValuePair<string, string> kvp in headers)
					h[kvp.Key] = kvp.Value;
			return McpServer.Handle(new McpHttpRequest()
			{
				HttpMethod = httpMethod,
				GetHeader = name => h.TryGetValue(name, out string v) ? v : null,
				Body = body == null ? null : (body is string s ? s : JsonConvert.SerializeObject(body)),
				RemoteIPAddress = "127.0.0.1",
				IsLoopback = loopback,
				IsHttps = false
			});
		}
		private static JObject Rpc(string token, string method, object parameters, string protocolVersion = "2025-11-25", int id = 1)
		{
			McpHttpResponse r = Post(token, new { jsonrpc = "2.0", id, method, @params = parameters }, new Dictionary<string, string>() { ["MCP-Protocol-Version"] = protocolVersion });
			Assert.IsNotNull(r.Body, "Status " + r.Status);
			return ParseJson(r.Body);
		}
		/// <summary>
		/// Calls a tool and returns the JSON-RPC result (CallToolResult).
		/// </summary>
		private static JObject CallRaw(string token, string tool, object args)
		{
			JObject msg = Rpc(token, "tools/call", new { name = tool, arguments = args });
			Assert.IsNull(msg["error"], "Unexpected JSON-RPC error: " + msg["error"]);
			return (JObject)msg["result"];
		}
		/// <summary>
		/// Calls a tool that must succeed and returns its structured result.
		/// </summary>
		private static JObject Call(string token, string tool, object args)
		{
			JObject result = CallRaw(token, tool, args);
			Assert.IsFalse(result.Value<bool>("isError"), "Tool error: " + result["content"]?[0]?["text"]);
			JObject structured = (JObject)result["structuredContent"];
			// MCP-9: the text content holds the same JSON.
			Assert.IsTrue(JToken.DeepEquals(structured, ParseJson(result["content"][0].Value<string>("text"))));
			return structured;
		}
		/// <summary>
		/// Calls a tool that must fail with a tool error and returns the error text.
		/// </summary>
		private static string CallError(string token, string tool, object args)
		{
			JObject result = CallRaw(token, tool, args);
			Assert.IsTrue(result.Value<bool>("isError"), "Expected a tool error but got: " + result);
			return result["content"][0].Value<string>("text");
		}
		private static long AddEvents(string project, params Event[] events)
		{
			using (DB db = new DB(project))
				foreach (Event e in events)
					db.AddEvent(e);
			return events.Last().EventId;
		}
		#endregion

		#region Real HTTP
		private class HttpResult
		{
			public int Status;
			public string Body;
			public System.Net.WebHeaderCollection Headers;
		}
		private static HttpResult Http(int port, string method, string path, string body = null, Dictionary<string, string> headers = null)
		{
			System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("http://127.0.0.1:" + port + "/" + path);
			req.Method = method;
			req.Proxy = null;
			req.AllowAutoRedirect = false;
			if (headers != null)
				foreach (KeyValuePair<string, string> h in headers)
					req.Headers[h.Key] = h.Value;
			if (body != null)
			{
				req.ContentType = "application/json";
				byte[] data = System.Text.Encoding.UTF8.GetBytes(body);
				using (System.IO.Stream s = req.GetRequestStream())
					s.Write(data, 0, data.Length);
			}
			System.Net.HttpWebResponse res;
			try
			{
				res = (System.Net.HttpWebResponse)req.GetResponse();
			}
			catch (System.Net.WebException ex) when (ex.Response != null)
			{
				res = (System.Net.HttpWebResponse)ex.Response;
			}
			using (res)
			using (System.IO.StreamReader reader = new System.IO.StreamReader(res.GetResponseStream()))
				return new HttpResult() { Status = (int)res.StatusCode, Body = reader.ReadToEnd(), Headers = res.Headers };
		}
		/// <summary>
		/// Runs the real web server (routing, MCP controller, response headers) on a loopback port.
		/// </summary>
		[TestMethod]
		public void EndpointOverRealHttp()
		{
			Settings.data.postgresReady = true;
			// The service clears this flag after its startup work, which these tests don't run.
			typeof(ErrorTrackerSvc).GetProperty("LoadingDatabases").SetValue(null, false);
			System.Net.Sockets.TcpListener probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
			probe.Start();
			int port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
			probe.Stop();
			WebServer srv = new WebServer(null);
			try
			{
				srv.SetBindings(new BPUtil.SimpleHttp.HttpServerBase.Binding(BPUtil.SimpleHttp.AllowedConnectionTypes.http, (ushort)port, System.Net.IPAddress.Loopback));
				Thread.Sleep(200);
				Dictionary<string, string> auth = new Dictionary<string, string>() { ["Authorization"] = "Bearer " + adminToken, ["MCP-Protocol-Version"] = "2025-11-25" };

				HttpResult init = Http(port, "POST", "mcp", "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{},\"clientInfo\":{\"name\":\"t\",\"version\":\"1\"}}}", auth);
				Assert.AreEqual(200, init.Status, init.Body);
				StringAssert.StartsWith(init.Headers["Content-Type"], "application/json");
				Assert.IsNull(init.Headers["Mcp-Session-Id"], "Stateless: no session is issued.");
				Assert.AreEqual("2025-11-25", ParseJson(init.Body)["result"].Value<string>("protocolVersion"));

				HttpResult call = Http(port, "POST", "mcp", "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"search_events\",\"arguments\":{\"project\":\"" + MainProject + "\",\"groupBy\":\"hash\"}}}", auth);
				Assert.AreEqual(200, call.Status, call.Body);
				Assert.IsFalse(ParseJson(call.Body)["result"].Value<bool>("isError"), call.Body);

				HttpResult note = Http(port, "POST", "mcp", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", auth);
				Assert.AreEqual(202, note.Status);
				Assert.AreEqual("", note.Body);

				Assert.AreEqual(405, Http(port, "GET", "mcp").Status);
				Assert.AreEqual(405, Http(port, "DELETE", "mcp", null, auth).Status);
				HttpResult unauthorized = Http(port, "POST", "mcp", "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
				Assert.AreEqual(401, unauthorized.Status);
				Assert.AreEqual("Bearer", unauthorized.Headers["WWW-Authenticate"]);
				Assert.AreEqual(404, Http(port, "GET", ".well-known/oauth-protected-resource").Status);
				Assert.AreEqual(404, Http(port, "GET", ".well-known/oauth-authorization-server").Status);
				Assert.AreEqual(404, Http(port, "GET", ".well-known/oauth-protected-resource/mcp").Status);

				Settings.data.mcpEnabled = false;
				Assert.AreEqual(404, Http(port, "POST", "mcp", "{}", auth).Status);
			}
			finally
			{
				srv.Stop();
			}
		}
		#endregion

		#region TEST-2 Tokens
		[TestMethod]
		public void TokensAreStoredOnlyAsHashes()
		{
			string token = ApiTokenManager.CreateToken(admin, true, "hash test", null, null, null, out ApiToken record, out string error);
			Assert.IsTrue(token.StartsWith("etk_") && token.Length >= 44, token);
			Assert.IsTrue(token.Substring(4).All(char.IsLetterOrDigit));
			using (GlobalDb db = new GlobalDb())
			{
				ApiToken stored = db.GetApiToken(record.ApiTokenId);
				Assert.AreEqual(ApiTokenManager.HashToken(token), stored.TokenHash);
				Assert.AreEqual(64, stored.TokenHash.Length);
				foreach (System.Reflection.PropertyInfo prop in typeof(ApiToken).GetProperties())
					Assert.AreNotEqual(token, prop.GetValue(stored)?.ToString(), prop.Name + " must not contain the plaintext token");
				Assert.AreEqual(token.Substring(0, 8), stored.TokenPrefix);
				Assert.AreEqual(ApiTokenScopes.EventsRead, stored.Scopes, "New tokens default to events:read only (AUTH-12).");
			}
		}
		[TestMethod]
		public void InvalidTokensGetIdentical401Responses()
		{
			string revoked = NewToken(admin, "to revoke", null);
			using (GlobalDb db = new GlobalDb())
				db.RevokeApiToken(db.GetApiTokenByHash(ApiTokenManager.HashToken(revoked)).ApiTokenId);
			string expiring = NewToken(admin, "expiring", null, null, TimeUtil.GetTimeInMsSinceEpoch() + 300);
			Assert.AreEqual(200, Post(expiring, new { jsonrpc = "2.0", id = 1, method = "tools/list" }).StatusCode);
			Thread.Sleep(600);

			List<McpHttpResponse> responses = new List<McpHttpResponse>()
			{
				Post(null, new { jsonrpc = "2.0", id = 1, method = "tools/list" }),
				Post("etk_" + new string('a', 48), new { jsonrpc = "2.0", id = 1, method = "tools/list" }),
				Post(revoked, new { jsonrpc = "2.0", id = 1, method = "tools/list" }),
				Post(expiring, new { jsonrpc = "2.0", id = 1, method = "tools/list" })
			};
			foreach (McpHttpResponse r in responses)
			{
				Assert.AreEqual(401, r.StatusCode);
				Assert.AreEqual("Bearer", r.Headers.First(h => h.Key == "WWW-Authenticate").Value);
				Assert.AreEqual(responses[0].Body, r.Body, "The response must not reveal why the token was rejected.");
			}
		}
		[TestMethod]
		public void FailedTokenAttemptsAreRateLimited()
		{
			for (int i = 0; i < ApiTokenManager.MaxFailuresPerWindow; i++)
				Assert.AreEqual(401, Post("etk_bad", new { jsonrpc = "2.0", id = 1, method = "tools/list" }).StatusCode);
			McpHttpResponse r = Post(adminToken, new { jsonrpc = "2.0", id = 1, method = "tools/list" });
			Assert.AreEqual(429, r.StatusCode, "Even a valid token is not checked while the address is blocked.");
			Assert.IsTrue(r.Headers.Any(h => h.Key == "Retry-After"));
		}
		[TestMethod]
		public void ToolsListOnlyShowsToolsTheTokenCanUse()
		{
			string[] readTools = ((JArray)Rpc(readerToken, "tools/list", new { })["result"]["tools"]).Select(t => t.Value<string>("name")).ToArray();
			CollectionAssert.Contains(readTools, "search_events");
			CollectionAssert.Contains(readTools, "preview_filter");
			CollectionAssert.DoesNotContain(readTools, "create_filter");
			CollectionAssert.DoesNotContain(readTools, "run_filters");
			CollectionAssert.DoesNotContain(readTools, "get_job");

			string[] allTools = ((JArray)Rpc(adminToken, "tools/list", new { })["result"]["tools"]).Select(t => t.Value<string>("name")).ToArray();
			Assert.AreEqual(16, allTools.Length);

			string error = CallError(readerToken, "create_filter", new { project = MainProject, definition = new { name = "x", enabled = false, conditionHandling = "Unconditional", conditions = new object[0], actions = new object[0] } });
			StringAssert.Contains(error, "filters:write");

			// MCP-10: every tool declares all annotations.
			foreach (JObject tool in (JArray)Rpc(adminToken, "tools/list", new { })["result"]["tools"])
			{
				JObject a = (JObject)tool["annotations"];
				foreach (string key in new string[] { "title", "readOnlyHint", "destructiveHint", "idempotentHint", "openWorldHint" })
					Assert.IsNotNull(a[key], tool.Value<string>("name") + " lacks annotation " + key);
				Assert.IsFalse(a.Value<bool>("openWorldHint"));
				Assert.AreEqual(false, tool["inputSchema"]["additionalProperties"]?.Value<bool>());
				Assert.IsNotNull(tool["outputSchema"]);
			}
		}
		[TestMethod]
		public void EffectiveAccessIsTheIntersectionOfUserAccessAndTokenProjects()
		{
			string mainOnly = NewToken(admin, "main only", null, new string[] { MainProject });
			JArray projects = (JArray)Call(mainOnly, "list_projects", new { })["projects"];
			Assert.AreEqual(1, projects.Count);
			Assert.AreEqual(MainProject, projects[0].Value<string>("name"));
			StringAssert.Contains(CallError(mainOnly, "list_folders", new { project = OtherProject }), "not found");

			// The reader has read-only access to the main project and no access to the other one.
			JArray readerProjects = (JArray)Call(readerToken, "list_projects", new { })["projects"];
			Assert.AreEqual(1, readerProjects.Count);
			Assert.AreEqual("read", readerProjects[0].Value<string>("access"));
			StringAssert.Contains(CallError(readerToken, "search_events", new { project = OtherProject }), "not found");

			// A token cannot have more access than its user has right now.
			User temp = new User("mcptemp", "pw", null, false);
			Settings.data.TryAddUser(temp);
			temp.InitializeUserId();
			temp.AllowProject(MainProject);
			string tempToken = NewToken(temp, "temp", new string[] { ApiTokenScopes.EventsRead, ApiTokenScopes.FiltersWrite });
			Call(tempToken, "list_folders", new { project = MainProject });
			temp.DisallowProject(MainProject);
			StringAssert.Contains(CallError(tempToken, "list_folders", new { project = MainProject }), "not found");
			temp.AllowProjectReadOnly(MainProject);
			Call(tempToken, "list_folders", new { project = MainProject });
			StringAssert.Contains(CallError(tempToken, "create_filter", new { project = MainProject, definition = new { name = "x", enabled = false, conditionHandling = "Unconditional", conditions = new object[0], actions = new object[0] } }), "read-only");

			// Renaming or deleting the user invalidates the token.
			temp.Name = "mcptemp2";
			Assert.AreEqual(401, Post(tempToken, new { jsonrpc = "2.0", id = 1, method = "tools/list" }).StatusCode);
			Settings.data.TryRemoveUser("mcptemp2");
		}
		[TestMethod]
		public void OnlyAdminsCanGrantSensitiveRead()
		{
			Assert.IsNull(ApiTokenManager.CreateToken(reader, false, "x", new string[] { ApiTokenScopes.SensitiveRead }, null, null, out ApiToken t, out string error));
			StringAssert.Contains(error, "administrator");
			Assert.IsNull(ApiTokenManager.CreateToken(reader, false, "x", null, new string[] { OtherProject }, null, out t, out error), "A token can't be limited to a project the user can't access.");
		}
		#endregion

		#region TEST-3 JSON-RPC and transport
		[TestMethod]
		public void InitializeNegotiatesTheProtocolVersion()
		{
			JObject r = Rpc(adminToken, "initialize", new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "test", version = "1" } });
			Assert.AreEqual("2025-06-18", r["result"].Value<string>("protocolVersion"));
			Assert.AreEqual(false, r["result"]["capabilities"]["tools"].Value<bool>("listChanged"));
			Assert.AreEqual("ErrorTracker", r["result"]["serverInfo"].Value<string>("name"));
			StringAssert.Contains(r["result"].Value<string>("instructions"), "untrusted");

			r = Rpc(adminToken, "initialize", new { protocolVersion = "1999-01-01" });
			Assert.AreEqual("2025-11-25", r["result"].Value<string>("protocolVersion"));
			r = Rpc(adminToken, "initialize", new { protocolVersion = "2025-03-26" });
			Assert.AreEqual("2025-03-26", r["result"].Value<string>("protocolVersion"));
		}
		[TestMethod]
		public void TransportBasics()
		{
			// Notifications and client responses get 202 with no body.
			McpHttpResponse r = Post(adminToken, new { jsonrpc = "2.0", method = "notifications/initialized" });
			Assert.AreEqual(202, r.StatusCode);
			Assert.IsNull(r.Body);
			Assert.AreEqual(202, Post(adminToken, new { jsonrpc = "2.0", id = 5, result = new { } }).StatusCode);

			// GET and DELETE are not allowed.
			Assert.AreEqual(405, Post(adminToken, null, null, "GET").StatusCode);
			Assert.AreEqual(405, Post(adminToken, null, null, "DELETE").StatusCode);

			// Batches are rejected.
			r = Post(adminToken, "[{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}]");
			Assert.AreEqual(-32600, ParseJson(r.Body)["error"].Value<int>("code"));

			// Malformed JSON.
			r = Post(adminToken, "{not json");
			Assert.AreEqual(-32700, ParseJson(r.Body)["error"].Value<int>("code"));

			// Origin validation.
			r = Post(adminToken, new { jsonrpc = "2.0", id = 1, method = "ping" }, new Dictionary<string, string>() { ["Origin"] = "https://evil.example" });
			Assert.AreEqual(403, r.StatusCode);
			Settings.data.mcpAllowedOrigins = new string[] { "https://evil.example" };
			Assert.AreEqual(200, Post(adminToken, new { jsonrpc = "2.0", id = 1, method = "ping" }, new Dictionary<string, string>() { ["Origin"] = "https://evil.example" }).StatusCode);

			// HTTPS is required except from loopback.
			Assert.AreEqual(403, Post(adminToken, new { jsonrpc = "2.0", id = 1, method = "ping" }, null, "POST", false).StatusCode);

			// Unknown methods and tools.
			Assert.AreEqual(-32601, Rpc(adminToken, "resources/list", new { })["error"].Value<int>("code"));
			JObject unknownTool = Rpc(adminToken, "tools/call", new { name = "no_such_tool", arguments = new { } });
			Assert.AreEqual(-32602, unknownTool["error"].Value<int>("code"));

			// Schema-invalid arguments are tool errors (not JSON-RPC errors) so the model can correct them (SEP-1303), and every problem is listed.
			string invalid = CallError(adminToken, "search_events", new { project = MainProject, limit = 5000, bogus = 1, conditions = new[] { new { tagKey = "URL", @operator = "Like", query = "x" } } });
			StringAssert.Contains(invalid, "`limit`: must be at most 500.");
			StringAssert.Contains(invalid, "`bogus`: is not an allowed property.");
			StringAssert.Contains(invalid, "`conditions[0].operator`: must be one of: \"Contains\", \"Equals\", \"StartsWith\", \"EndsWith\".");
			StringAssert.Contains(CallError(adminToken, "search_events", new { }), "`project`: is required.");
			// A malformed request (arguments that are not an object) is still a protocol error.
			Assert.AreEqual(-32602, Rpc(adminToken, "tools/call", new { name = "search_events", arguments = 5 })["error"].Value<int>("code"));

			// Execution errors are tool results with isError, telling the agent how to recover.
			StringAssert.Contains(CallError(adminToken, "list_folders", new { project = "nope" }), "list_projects");

			// An unsupported version header is rejected.
			r = Post(adminToken, new { jsonrpc = "2.0", id = 1, method = "tools/list" }, new Dictionary<string, string>() { ["MCP-Protocol-Version"] = "2024-11-05" });
			Assert.AreEqual(400, r.StatusCode);
			Assert.AreEqual(-32022, ParseJson(r.Body)["error"].Value<int>("code"));

			// A request without the header is treated as 2025-03-26.
			Assert.AreEqual(200, Post(adminToken, new { jsonrpc = "2.0", id = 1, method = "tools/list" }).StatusCode);

			// Disabled: 404.
			Settings.data.mcpEnabled = false;
			Assert.AreEqual(404, Post(adminToken, new { jsonrpc = "2.0", id = 1, method = "ping" }).StatusCode);
		}
		[TestMethod]
		public void ModernProtocolRevision()
		{
			Dictionary<string, string> headers = new Dictionary<string, string>() { ["MCP-Protocol-Version"] = McpServer.ModernVersion, ["Mcp-Method"] = "server/discover" };
			object meta = new Dictionary<string, object>() { [McpServer.MetaProtocolVersion] = McpServer.ModernVersion, ["io.modelcontextprotocol/clientCapabilities"] = new { } };
			McpHttpResponse r = Post(adminToken, new { jsonrpc = "2.0", id = 1, method = "server/discover", @params = new { _meta = meta } }, headers);
			Assert.AreEqual(200, r.StatusCode, r.Body);
			JObject result = (JObject)ParseJson(r.Body)["result"];
			Assert.AreEqual("complete", result.Value<string>("resultType"));
			CollectionAssert.Contains(result["supportedVersions"].Values<string>().ToArray(), "2026-07-28");
			CollectionAssert.Contains(result["supportedVersions"].Values<string>().ToArray(), "2025-11-25");
			Assert.AreEqual("ErrorTracker", result["_meta"][McpServer.MetaServerInfo].Value<string>("name"));

			// tools/call with matching headers.
			headers["Mcp-Method"] = "tools/call";
			headers["Mcp-Name"] = "list_projects";
			r = Post(adminToken, new { jsonrpc = "2.0", id = 2, method = "tools/call", @params = new { name = "list_projects", arguments = new { }, _meta = meta } }, headers);
			Assert.AreEqual(200, r.StatusCode, r.Body);
			Assert.AreEqual("complete", ParseJson(r.Body)["result"].Value<string>("resultType"));

			// Base64-encoded Mcp-Name is decoded before comparison.
			headers["Mcp-Name"] = "=?base64?" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("list_projects")) + "?=";
			Assert.AreEqual(200, Post(adminToken, new { jsonrpc = "2.0", id = 2, method = "tools/call", @params = new { name = "list_projects", arguments = new { }, _meta = meta } }, headers).StatusCode);

			// Header mismatch.
			headers["Mcp-Name"] = "search_events";
			r = Post(adminToken, new { jsonrpc = "2.0", id = 3, method = "tools/call", @params = new { name = "list_projects", arguments = new { }, _meta = meta } }, headers);
			Assert.AreEqual(400, r.StatusCode);
			Assert.AreEqual(-32020, ParseJson(r.Body)["error"].Value<int>("code"));

			// Unknown methods are 404 with -32601 (ping was removed in this revision).
			headers["Mcp-Method"] = "ping";
			r = Post(adminToken, new { jsonrpc = "2.0", id = 4, method = "ping", @params = new { _meta = meta } }, headers);
			Assert.AreEqual(404, r.StatusCode);
			Assert.AreEqual(-32601, ParseJson(r.Body)["error"].Value<int>("code"));

			// Unsupported version in _meta.
			object badMeta = new Dictionary<string, object>() { [McpServer.MetaProtocolVersion] = "2099-01-01" };
			r = Post(adminToken, new { jsonrpc = "2.0", id = 5, method = "tools/list", @params = new { _meta = badMeta } }, new Dictionary<string, string>() { ["MCP-Protocol-Version"] = "2099-01-01", ["Mcp-Method"] = "tools/list" });
			Assert.AreEqual(400, r.StatusCode);
			JObject err = (JObject)ParseJson(r.Body)["error"];
			Assert.AreEqual(-32022, err.Value<int>("code"));
			Assert.AreEqual("2099-01-01", err["data"].Value<string>("requested"));

			// tools/list carries cache hints.
			r = Post(adminToken, new { jsonrpc = "2.0", id = 6, method = "tools/list", @params = new { _meta = meta } }, new Dictionary<string, string>() { ["MCP-Protocol-Version"] = McpServer.ModernVersion, ["Mcp-Method"] = "tools/list" });
			JObject list = (JObject)ParseJson(r.Body)["result"];
			Assert.IsNotNull(list["ttlMs"]);
			Assert.AreEqual("private", list.Value<string>("cacheScope"));
		}
		#endregion

		#region TEST-4 Redaction
		[TestMethod]
		public void DefaultRedactionPatterns()
		{
			McpRedactor r = new McpRedactor("", McpRedactionRule.GetDefaults());
			string headers = "Host: example.com\r\nCookie: ASP.NET_SessionId=abc123; auth=xyz\r\nAuthorization: Basic dXNlcjpwYXNz\r\nX-Api-Key: k-123\r\nUser-Agent: test";
			string redacted = r.RedactText(headers);
			Assert.IsFalse(redacted.Contains("abc123") || redacted.Contains("xyz") || redacted.Contains("dXNlcjpwYXNz") || redacted.Contains("k-123"), redacted);
			StringAssert.Contains(redacted, "Cookie: [REDACTED]");
			StringAssert.Contains(redacted, "Host: example.com");
			StringAssert.Contains(redacted, "User-Agent: test");

			string allHttp = "HTTP_CONNECTION:keep-alive\r\nHTTP_COOKIE:session=s3cr3t; other=1\r\nHTTP_AUTHORIZATION:Bearer abc.def\r\nHTTP_X_API_KEY:zzz\r\nHTTP_HOST:example.com";
			redacted = r.RedactTag("All HTTP", allHttp);
			Assert.IsFalse(redacted.Contains("s3cr3t") || redacted.Contains("abc.def") || redacted.Contains("zzz"), redacted);
			StringAssert.Contains(redacted, "HTTP_HOST:example.com");

			redacted = r.RedactText("{\"user\":\"bob\",\"password\":\"hunter2\",\"apiKey\":\"q1w2\",\"nested\":{\"client_secret\":\"s\\\"x\"}}");
			Assert.IsFalse(redacted.Contains("hunter2") || redacted.Contains("q1w2") || redacted.Contains("s\\\"x"), redacted);
			StringAssert.Contains(redacted, "\"user\":\"bob\"");

			redacted = r.RedactText("POST user=bob&password=hunter2&token=t0k Server=db;Password=pg;Database=x Authorization header: Bearer eyJhbGciOi.abc etk_ABCDEFGHIJKLMNOPQRSTUVWXYZ0123");
			Assert.IsFalse(redacted.Contains("hunter2") || redacted.Contains("t0k") || redacted.Contains("=pg") || redacted.Contains("eyJhbGciOi") || redacted.Contains("ABCDEFGHIJKLMNOP"), redacted);
			StringAssert.Contains(redacted, "user=bob");
		}
		[TestMethod]
		public void HiddenTagsAndSensitiveRead()
		{
			string project = NewProject();
			long id = AddEvents(project, MakeEvent(EventType.Error, "Web", "Request failed", TimeUtil.GetTimeInMsSinceEpoch(), new Dictionary<string, string>()
			{
				["All HTTP"] = "HTTP_COOKIE:session=s3cr3t\r\nHTTP_HOST:example.com",
				["Secret Tag"] = "classified",
				["URL"] = "https://example.com/"
			}));
			Settings.data.mcpHiddenTagKeys = "secret tag";
			string limited = NewToken(admin, "no sensitive", null);

			JObject ev = (JObject)Call(limited, "get_events", new { project, eventIds = new[] { id } })["events"][0];
			string[] keys = ev["tags"].Select(t => t.Value<string>("key")).ToArray();
			CollectionAssert.DoesNotContain(keys, "Secret Tag");
			CollectionAssert.Contains(ev["redactedTags"].Values<string>().ToArray(), "Secret Tag");
			string allHttp = ev["tags"].First(t => t.Value<string>("key") == "All HTTP").Value<string>("value");
			Assert.IsFalse(allHttp.Contains("s3cr3t"), allHttp);
			StringAssert.Contains(allHttp, "[REDACTED]");

			// Conditions on hidden tags are rejected (RED-4), in searches, previews and stats.
			StringAssert.Contains(CallError(limited, "search_events", new { project, conditions = new[] { new { tagKey = "SECRET TAG", @operator = "Contains", query = "c" } } }), "hides");
			StringAssert.Contains(CallError(limited, "preview_filter", new { project, definition = new { name = "p", enabled = false, conditionHandling = "All", conditions = new[] { new { tagKey = "Secret Tag", @operator = "Contains", query = "c" } }, actions = new object[0] } }), "hides");
			StringAssert.Contains(CallError(limited, "event_stats", new { project, groupBy = new { tag = "Secret Tag" } }), "hides");
			// ...and a substring search can't reach them either.
			Assert.AreEqual(0, ((JArray)Call(limited, "search_events", new { project, contains = "classified" })["events"]).Count);
			Assert.AreEqual(1, ((JArray)Call(adminToken, "search_events", new { project, contains = "classified" })["events"]).Count);

			// sensitive:read bypasses all of this.
			ev = (JObject)Call(adminToken, "get_events", new { project, eventIds = new[] { id } })["events"][0];
			Assert.IsNull(ev["redactedTags"]);
			StringAssert.Contains(ev["tags"].First(t => t.Value<string>("key") == "All HTTP").Value<string>("value"), "s3cr3t");
			Assert.AreEqual("classified", ev["tags"].First(t => t.Value<string>("key") == "Secret Tag").Value<string>("value"));
			Call(adminToken, "search_events", new { project, conditions = new[] { new { tagKey = "Secret Tag", @operator = "Contains", query = "c" } } });
		}
		#endregion

		#region TEST-5 Output
		[TestMethod]
		public void HtmlToPlainTextHandlesAstralEntities()
		{
			Assert.AreEqual("smile \U0001F600 and \U0001F600 ok", HtmlMessageSanitizer.ToPlainText("<b>smile</b> &#128512; and &#x1F600; ok"));
			Assert.AreEqual("a\n\nb\nc", HtmlMessageSanitizer.ToPlainText("<p>a</p><div>b<br>c</div>", int.MaxValue, true));
		}
		[TestMethod]
		public void EventOutput()
		{
			string project = NewProject();
			string longText = new string('x', 5000);
			string html = "<p>Hello <b>world</b> &#128512;</p><img src=\"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk\">" + "<pre>at Foo()\nat Bar()</pre>";
			long longId = AddEvents(project, MakeEvent(EventType.Error, "Long", longText, TimeUtil.GetTimeInMsSinceEpoch() - 1000, new Dictionary<string, string>() { ["Big"] = longText }));
			long htmlId = AddEvents(project, MakeEvent(EventType.Error, "Html", html, TimeUtil.GetTimeInMsSinceEpoch(), null, true));

			JArray events = (JArray)Call(adminToken, "get_events", new { project, eventIds = new[] { longId, htmlId, 999999 }, maxMessageChars = 100, maxTagChars = 50 })["events"];
			JObject longEv = (JObject)events.First(e => e.Value<long>("eventId") == longId);
			Assert.AreEqual(100, longEv.Value<string>("message").Length);
			Assert.AreEqual(5000, longEv.Value<int>("messageOriginalLength"));
			JObject bigTag = (JObject)longEv["tags"][0];
			Assert.AreEqual(50, bigTag.Value<string>("value").Length);
			Assert.AreEqual(5000, bigTag.Value<int>("originalLength"));
			Assert.IsTrue(longEv.Value<string>("date").EndsWith("Z"));
			Assert.AreEqual("https://errors.example.com/client/home?p=" + project + "&f=0&e=" + longId, longEv.Value<string>("webUrl"));

			JObject htmlEv = (JObject)events.First(e => e.Value<long>("eventId") == htmlId);
			Assert.IsTrue(htmlEv.Value<bool>("messageWasHtml"));
			string text = htmlEv.Value<string>("message");
			StringAssert.Contains(text, "Hello world \U0001F600");
			StringAssert.Contains(text, "at Foo()\nat Bar()");
			Assert.IsFalse(text.Contains("<") || text.Contains("base64"), text);
			Assert.IsFalse(htmlEv.Value<string>("messageSearchPrefix").Contains("iVBORw0KGgo"), "data: URIs are never returned");

			JObject all = Call(adminToken, "get_events", new { project, eventIds = new[] { 999999 } });
			CollectionAssert.AreEqual(new long[] { 999999 }, all["notFound"].Values<long>().ToArray());
		}
		[TestMethod]
		public void GetEventsDoesNotChangeReadState()
		{
			string project = NewProject();
			long id = AddEvents(project, MakeEvent(EventType.Error, "Read", "read state", TimeUtil.GetTimeInMsSinceEpoch(), null));
			Call(adminToken, "get_events", new { project, eventIds = new[] { id } });
			using (DB db = new DB(project))
				Assert.IsFalse(db.ExistsReadState(admin.UserId, id));
			Assert.IsFalse(Call(adminToken, "search_events", new { project })["events"][0].Value<bool>("read"));
			Call(adminToken, "get_events", new { project, eventIds = new[] { id }, markRead = true });
			using (DB db = new DB(project))
				Assert.IsTrue(db.ExistsReadState(admin.UserId, id));
			Assert.IsTrue(Call(adminToken, "search_events", new { project })["events"][0].Value<bool>("read"));
		}
		[TestMethod]
		public void ResponseBudgetTruncatesWithValidJsonAndCursor()
		{
			string project = NewProject();
			long now = TimeUtil.GetTimeInMsSinceEpoch();
			List<Event> events = new List<Event>();
			for (int i = 0; i < 200; i++)
				events.Add(MakeEvent(EventType.Error, "Budget " + i, "Message " + i + " " + new string('m', 400), now - i * 1000, new Dictionary<string, string>() { ["Tag"] = new string('t', 500) }));
			AddEvents(project, events.ToArray());
			McpHttpResponse r = Post(adminToken, new { jsonrpc = "2.0", id = 1, method = "tools/call", @params = new { name = "search_events", arguments = new { project, limit = 200, includeTags = new[] { "Tag" } } } });
			Assert.IsTrue(r.Body.Length < McpOutput.ResponseBudgetChars * 2 + 5000, "The text and structured copies are each within the budget; total was " + r.Body.Length);
			JObject result = (JObject)ParseJson(r.Body)["result"]["structuredContent"];
			Assert.IsTrue(result.Value<bool>("truncated"));
			Assert.IsNotNull(result["nextCursor"]);
			int firstPage = ((JArray)result["events"]).Count;
			Assert.IsTrue(firstPage > 0 && firstPage < 200);

			// Following the cursor returns the rest, each event once.
			HashSet<long> seen = new HashSet<long>(result["events"].Select(e => e.Value<long>("eventId")));
			string cursor = result.Value<string>("nextCursor");
			while (cursor != null)
			{
				JObject page = Call(adminToken, "search_events", new { project, limit = 200, includeTags = new[] { "Tag" }, cursor });
				foreach (JToken e in page["events"])
					Assert.IsTrue(seen.Add(e.Value<long>("eventId")), "Duplicate event " + e["eventId"]);
				cursor = page.Value<string>("nextCursor");
			}
			Assert.AreEqual(200, seen.Count);

			// A cursor can't be reused with different arguments.
			StringAssert.Contains(CallError(adminToken, "search_events", new { project, limit = 200, cursor = result.Value<string>("nextCursor") }), "different arguments");
		}
		#endregion

		#region TEST-6 Search
		[TestMethod]
		public void KeysetPagingReturnsEachEventOnceWhileNewEventsArrive()
		{
			string project = NewProject();
			long now = TimeUtil.GetTimeInMsSinceEpoch();
			List<Event> events = new List<Event>();
			for (int i = 0; i < 25; i++)
				events.Add(MakeEvent(EventType.Info, "Page", "Paged event " + i, now - 10000 - (i / 2) * 1000, null)); // Pairs share a Date.
			AddEvents(project, events.ToArray());
			HashSet<long> seen = new HashSet<long>();
			string cursor = null;
			int pages = 0;
			do
			{
				JObject page = cursor == null
					? Call(adminToken, "search_events", new { project, limit = 7 })
					: Call(adminToken, "search_events", new { project, limit = 7, cursor });
				foreach (JToken e in page["events"])
					Assert.IsTrue(seen.Add(e.Value<long>("eventId")));
				cursor = page.Value<string>("nextCursor");
				// New events arrive between pages; they are newer than the cursor, so they don't disturb paging.
				AddEvents(project, MakeEvent(EventType.Info, "Page", "New event " + pages, TimeUtil.GetTimeInMsSinceEpoch(), null));
				pages++;
			}
			while (cursor != null);
			Assert.AreEqual(25, seen.Count, "Every original event exactly once; events that arrived later are newer than the cursor and are not included.");
			Assert.AreEqual(4, pages);
		}
		[TestMethod]
		public void DateRangesAndTagKeyCase()
		{
			string project = NewProject();
			DateTime t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			long ms0 = TimeUtil.GetTimeInMsSinceEpoch(t0);
			AddEvents(project,
				MakeEvent(EventType.Error, "A", "at t0", ms0, new Dictionary<string, string>() { ["Session ID"] = "S-1" }),
				MakeEvent(EventType.Error, "B", "at t0+1h", ms0 + 3600000, new Dictionary<string, string>() { ["session id"] = "S-2" }),
				MakeEvent(EventType.Error, "C", "at t0+2h", ms0 + 7200000, null));

			// since is inclusive, until is exclusive.
			JArray range = (JArray)Call(adminToken, "search_events", new { project, since = "2026-01-01T00:00:00Z", until = "2026-01-01T02:00:00Z" })["events"];
			CollectionAssert.AreEquivalent(new[] { "A", "B" }, range.Select(e => e.Value<string>("subType")).ToArray());

			// Tag-key matching is case-insensitive, as in filters.
			JArray tagged = (JArray)Call(adminToken, "search_events", new { project, since = "all", conditions = new[] { new { tagKey = "SESSION ID", @operator = "StartsWith", query = "s-" } } })["events"];
			Assert.AreEqual(2, tagged.Count);
			// A missing tag counts as an empty string: the inverted condition matches the event without the tag.
			JArray inverted = (JArray)Call(adminToken, "search_events", new { project, since = "all", conditions = new[] { new { tagKey = "Session ID", @operator = "Contains", query = "S-", invert = true } } })["events"];
			Assert.AreEqual("C", inverted.Single().Value<string>("subType"));

			// Date and Folder conditions are rejected with a hint.
			StringAssert.Contains(CallError(adminToken, "search_events", new { project, conditions = new[] { new { tagKey = "Date", @operator = "Contains", query = "2026" } } }), "since");
			// Default range is 30 days.
			JObject defaulted = Call(adminToken, "search_events", new { project });
			Assert.AreEqual(0, ((JArray)defaulted["events"]).Count);
			Assert.IsTrue(defaulted.Value<string>("appliedSince").EndsWith("Z"));
			// Relative and invalid times.
			StringAssert.Contains(CallError(adminToken, "search_events", new { project, since = "yesterday-ish" }), "ISO 8601");
		}
		[TestMethod]
		public void GroupingAndStatistics()
		{
			JObject grouped = Call(adminToken, "search_events", new { project = MainProject, groupBy = "hash" });
			JObject rateLimit = (JObject)grouped["groups"].First(g => g["sample"].Value<string>("subType") == "Login Rate Limit");
			Assert.AreEqual(2, rateLimit.Value<int>("count"));

			JObject text = Call(adminToken, "search_events", new { project = MainProject, text = "NullRef" });
			Assert.AreEqual(0, ((JArray)text["events"]).Count, "Full-text search matches words, not substrings.");
			JObject contains = Call(adminToken, "search_events", new { project = MainProject, contains = "NullRef" });
			Assert.AreEqual(1, ((JArray)contains["events"]).Count);

			JObject stats = Call(adminToken, "event_stats", new { project = MainProject, groupBy = "subType", bucket = "hour", since = "1d" });
			JObject rl = (JObject)stats["groups"].First(g => g.Value<string>("key") == "Login Rate Limit");
			Assert.AreEqual(2, rl.Value<int>("count"));
			Assert.AreEqual(2, ((JArray)rl["buckets"]).Count);
			Assert.IsTrue(rl["buckets"].All(b => b.Value<string>("start").EndsWith(":00:00.000Z")), "Buckets are aligned to UTC hours.");
			Assert.AreEqual(3, stats.Value<int>("totalMatching"));

			// onlyNewSince: the "Old" error first appeared 60 days ago, so it is not new.
			string project = NewProject();
			long now = TimeUtil.GetTimeInMsSinceEpoch();
			AddEvents(project,
				MakeEvent(EventType.Error, "Recurring", "Seen long ago", now - 40L * 86400000, null),
				MakeEvent(EventType.Error, "Recurring", "Seen long ago", now - 3600000, null),
				MakeEvent(EventType.Error, "Fresh", "Brand new", now - 7200000, null));
			JObject fresh = Call(adminToken, "event_stats", new { project, groupBy = "hash", onlyNewSince = "24h" });
			Assert.AreEqual("Brand new", fresh["groups"].Single().Value<string>("sampleMessagePreview"));
			Assert.IsNotNull(fresh["groups"][0]["firstSeenEver"]);

			// Statistics by event type and by tag.
			JObject byType = Call(adminToken, "event_stats", new { project = MainProject, groupBy = "eventType", since = "all" });
			CollectionAssert.AreEquivalent(new[] { "Error", "Info" }, byType["groups"].Select(g => g.Value<string>("key")).ToArray());
			JObject byTag = Call(adminToken, "event_stats", new { project = MainProject, groupBy = new { tag = "ip" }, since = "1d" });
			CollectionAssert.AreEquivalent(new[] { "10.0.0.1", "10.0.0.2", "" }, byTag["groups"].Select(g => g.Value<string>("key")).ToArray());
		}
		#endregion

		#region TEST-1 Preview parity
		private class ParityCase
		{
			public string Name;
			public object Definition;
			public int ExpectedMatches;
			/// <summary>
			/// True if create_filter rejects the definition, so only the preview can be checked.
			/// </summary>
			public bool PreviewOnly;
		}
		/// <summary>
		/// Events used by the parity test.  Tags: "Kind" on some events only.
		/// </summary>
		private static Event[] ParityEvents()
		{
			long now = TimeUtil.GetTimeInMsSinceEpoch();
			return new Event[]
			{
				MakeEvent(EventType.Error, "Alpha", "Timeout while calling Billing", now - 1000, new Dictionary<string, string>() { ["Kind"] = "Network" }),
				MakeEvent(EventType.Error, "Beta", "NullReferenceException in Foo", now - 2000, new Dictionary<string, string>() { ["kind"] = "Code" }),
				MakeEvent(EventType.Info, "Login Rate Limit", "Too many logins", now - 3000, null),
				MakeEvent(EventType.Debug, "Gamma", "<b>HTML</b> body", now - 4000, null, true),
				MakeEvent(EventType.Info, "alpha", "lower-case subtype", now - 5000, new Dictionary<string, string>() { ["KIND"] = "" })
			};
		}
		private static object Def(string handling, object[] conditions, object[] actions)
		{
			return new { name = "parity", enabled = true, conditionHandling = handling, conditions, actions };
		}
		private static object C(string tagKey, string op, string query, bool regex = false, bool invert = false, bool enabled = true)
		{
			return new { tagKey, @operator = op, query, regex, invert, enabled };
		}
		private static object A(string type, string argument = null)
		{
			return argument == null ? (object)new { type } : new { type, argument };
		}
		[TestMethod]
		public void PreviewMatchesRealRunsExactly()
		{
			object[] moveAndColor = new object[] { A("SetColor", "#FF0000"), A("MoveTo", "/Parity/Moved") };
			List<ParityCase> cases = new List<ParityCase>()
			{
				new ParityCase() { Name = "Contains is case-insensitive", Definition = Def("All", new[] { C("SubType", "Contains", "ALPHA") }, moveAndColor), ExpectedMatches = 2 },
				new ParityCase() { Name = "Equals", Definition = Def("All", new[] { C("SubType", "Equals", "alpha") }, moveAndColor), ExpectedMatches = 2 },
				new ParityCase() { Name = "StartsWith", Definition = Def("All", new[] { C("Message", "StartsWith", "time") }, moveAndColor), ExpectedMatches = 1 },
				new ParityCase() { Name = "EndsWith", Definition = Def("All", new[] { C("Message", "EndsWith", "FOO") }, moveAndColor), ExpectedMatches = 1 },
				new ParityCase() { Name = "Regex Equals is anchored", Definition = Def("All", new[] { C("SubType", "Equals", "alph", true) }, moveAndColor), ExpectedMatches = 0 },
				new ParityCase() { Name = "Regex Contains", Definition = Def("All", new[] { C("Message", "Contains", "null.*foo", true) }, moveAndColor), ExpectedMatches = 1 },
				new ParityCase() { Name = "Message sees raw HTML", Definition = Def("All", new[] { C("Message", "Contains", "<b>") }, moveAndColor), ExpectedMatches = 1 },
				new ParityCase() { Name = "Tag keys are case-insensitive", Definition = Def("All", new[] { C("kind", "Equals", "network") }, moveAndColor), ExpectedMatches = 1 },
				new ParityCase() { Name = "Missing tag is empty string (inverted)", Definition = Def("All", new[] { C("Kind", "Contains", "o", false, true) }, moveAndColor), ExpectedMatches = 3 },
				new ParityCase() { Name = "Empty tag value equals empty", Definition = Def("All", new[] { C("Kind", "Equals", "") }, moveAndColor), ExpectedMatches = 3 },
				new ParityCase() { Name = "Empty TagKey never matches", Definition = Def("Any", new[] { C(" ", "Contains", "") }, moveAndColor), ExpectedMatches = 0, PreviewOnly = true },
				new ParityCase() { Name = "All with every condition disabled never matches", Definition = Def("All", new[] { C("SubType", "Contains", "a", false, false, false) }, moveAndColor), ExpectedMatches = 0 },
				new ParityCase() { Name = "Any", Definition = Def("Any", new[] { C("EventType", "Equals", "Debug"), C("SubType", "StartsWith", "Login") }, moveAndColor), ExpectedMatches = 2 },
				new ParityCase() { Name = "All", Definition = Def("All", new[] { C("EventType", "Equals", "Error"), C("Kind", "Equals", "Code") }, moveAndColor), ExpectedMatches = 1 },
				new ParityCase() { Name = "Unconditional", Definition = Def("Unconditional", new object[0], new[] { A("MarkRead") }), ExpectedMatches = 5 },
				new ParityCase() { Name = "Color condition", Definition = Def("All", new[] { C("Color", "Equals", "EBEBEB") }, new[] { A("MarkUnread") }), ExpectedMatches = 5 },
				new ParityCase() { Name = "StopExecution ends processing", Definition = Def("All", new[] { C("EventType", "Equals", "Info") }, new[] { A("SetColor", "#00FF00"), A("StopExecution"), A("MoveTo", "/Parity/Never") }), ExpectedMatches = 2 },
				new ParityCase() { Name = "Delete ends processing", Definition = Def("All", new[] { C("SubType", "Equals", "Beta") }, new[] { A("Delete"), A("MoveTo", "/Parity/Never") }), ExpectedMatches = 1 },
				new ParityCase() { Name = "Invalid regex matches nothing", Definition = Def("All", new[] { C("Message", "Contains", "([", true) }, moveAndColor), ExpectedMatches = 0, PreviewOnly = true },
			};
			foreach (ParityCase c in cases)
			{
				string project = NewProject();
				AddEvents(project, ParityEvents());
				JObject preview = Call(adminToken, "preview_filter", new { project, definition = c.Definition, since = "all" });
				Assert.AreEqual(c.ExpectedMatches, preview.Value<int>("matched"), c.Name + ": preview matched");
				Assert.AreEqual(5, preview.Value<int>("evaluated"), c.Name);
				if (c.Name.StartsWith("Invalid regex"))
					Assert.IsTrue(preview["warnings"].Values<string>().Any(w => w.Contains("does not compile")));
				if (c.PreviewOnly)
					continue;

				// Now really run the same filter and compare the effects.
				JObject created = Call(writeToken, "create_filter", new { project, definition = c.Definition });
				int filterId = created["filter"].Value<int>("filterId");
				JObject run = Call(writeToken, "run_filters", new { project, filterId, allowDelete = true });
				Assert.AreEqual("completed", run.Value<string>("status"), c.Name);
				if (c.ExpectedMatches > 0)
					Assert.AreEqual(c.ExpectedMatches, run.Value<int>("matched"), c.Name + ": run matched");
				JObject pe = (JObject)preview["effects"];
				JObject re = (JObject)run["effects"];
				Assert.AreEqual(pe.Value<long?>("delete") ?? 0, re.Value<long>("deleted"), c.Name + ": deleted");
				Assert.AreEqual(pe["setColor"]?.Sum(x => x.Value<long>("count")) ?? 0, re.Value<long>("colored"), c.Name + ": colored");
				Assert.AreEqual(pe.Value<long?>("markRead") ?? 0, re.Value<long>("markedRead"), c.Name + ": markRead");
				Assert.AreEqual(pe.Value<long?>("markUnread") ?? 0, re.Value<long>("markedUnread"), c.Name + ": markUnread");
				Dictionary<string, long> previewMoves = (pe["moveTo"] as JArray ?? new JArray()).ToDictionary(m => m.Value<string>("path"), m => m.Value<long>("count"));
				Dictionary<string, long> runMoves = ((JArray)re["moved"]).ToDictionary(m => m.Value<string>("path"), m => m.Value<long>("count"));
				CollectionAssert.AreEquivalent(previewMoves.ToList(), runMoves.ToList(), c.Name + ": moves");
				if (previewMoves.Count > 0)
				{
					Assert.IsFalse(pe["moveTo"][0].Value<bool>("folderExists"), "The folder doesn't exist before the run.");
					Assert.IsTrue(preview["samples"].All(s => s.Value<string>("folderPath") == "/" && s.Value<string>("color") == "#EBEBEB"), c.Name + ": samples show events as they are now, not as the filter would leave them");
					CollectionAssert.Contains(re["foldersCreated"].Values<string>().ToArray(), "/Parity/Moved");
					Assert.IsTrue(preview["warnings"].Values<string>().Any(w => w.Contains("does not exist")), c.Name);
				}
				if (c.Name.StartsWith("Delete"))
					Assert.IsTrue(preview["warnings"].Values<string>().Any(w => w.Contains("Delete")));
				if (c.Name.StartsWith("Unconditional"))
					Assert.IsTrue(preview["warnings"].Values<string>().Any(w => w.Contains("every user")));
				if (c.Name.StartsWith("StopExecution"))
				{
					Assert.AreEqual(2, pe.Value<int>("stopExecution"));
					Assert.IsFalse(re["moved"].Any(), "Actions after StopExecution never run.");
				}
			}
		}
		[TestMethod]
		public void PreviewWarnsAboutInvertedConditionsOnMissingTags()
		{
			string project = NewProject();
			AddEvents(project, ParityEvents());
			JObject preview = Call(adminToken, "preview_filter", new { project, definition = Def("All", new[] { C("Kind", "Contains", "o", false, true) }, new[] { A("MarkRead") }), since = "all" });
			Assert.IsTrue(preview["warnings"].Values<string>().Any(w => w.Contains("do not have the tag")), string.Join("\n", preview["warnings"]));
			StringAssert.Contains(preview.Value<string>("evaluation"), "standalone");
			Assert.AreEqual(3, ((JArray)preview["samples"]).Count);
		}
		#endregion

		#region Schema
		/// <summary>
		/// DB version 12 widens Tag.TagId (and its identity sequence) to 64 bits.
		/// </summary>
		[TestMethod]
		public void TagIdIs64Bit()
		{
			using (DB db = new DB(MainProject))
			{
				string columnType = db.FetchSql<string>(PetaPoco.Sql.Builder.Append("SELECT data_type FROM information_schema.columns WHERE table_schema = @0 AND table_name = 'tag' AND column_name = 'tagid'", db.ProjectNameLower)).Single();
				Assert.AreEqual("bigint", columnType);
				string sequenceType = db.FetchSql<string>(PetaPoco.Sql.Builder.Append("SELECT data_type::text FROM pg_sequences WHERE schemaname = @0 AND sequencename = 'tag_tagid_seq'", db.ProjectNameLower)).Single();
				Assert.AreEqual("bigint", sequenceType);
				Assert.AreEqual(12, db.FetchSql<int>(PetaPoco.Sql.Builder.Append("SELECT CurrentVersion FROM " + db.ProjectNameLower + ".DbVersion")).Single());
			}
		}
		#endregion

		#region Pipeline preview
		[TestMethod]
		public void PipelinePreviewReportsInteractionsWithOtherFilters()
		{
			string project = NewProject();
			AddEvents(project, ParityEvents());
			// An existing filter colors Info events.
			int colorInfo = Call(writeToken, "create_filter", new { project, definition = new { name = "color info", enabled = true, conditionHandling = "All", conditions = new[] { C("EventType", "Equals", "Info") }, actions = new[] { A("SetColor", "#111111") } } })["filter"].Value<int>("filterId");
			object candidate = new { name = "candidate", enabled = false, conditionHandling = "All", conditions = new[] { C("SubType", "StartsWith", "Login") }, actions = new[] { A("MoveTo", "/Noise"), A("StopExecution") } };

			// Inserted first, the candidate's StopExecution keeps "color info" from coloring the login event.
			JObject first = Call(adminToken, "preview_filter", new { project, definition = candidate, mode = "pipeline", position = "first", since = "all" });
			Assert.AreEqual(5, first.Value<int>("evaluated"));
			Assert.AreEqual(1, first.Value<int>("candidateMatched"));
			Assert.AreEqual(1, first.Value<int>("changedEvents"));
			JObject changes = (JObject)first["changes"];
			Assert.AreEqual("/", changes["moved"][0].Value<string>("from"));
			Assert.AreEqual("/Noise", changes["moved"][0].Value<string>("to"));
			Assert.AreEqual("#EBEBEB", changes["colorChanged"][0].Value<string>("color"));
			Assert.AreEqual(colorInfo, changes["filtersNoLongerApplied"][0].Value<int>("filterId"));
			Assert.AreEqual(1, changes["filtersNoLongerApplied"][0].Value<int>("count"));
			JObject sample = (JObject)first["changedSamples"][0];
			Assert.AreEqual("#111111", sample["before"].Value<string>("color"));
			Assert.AreEqual("/Noise", sample["after"].Value<string>("folderPath"));
			Assert.IsTrue(first["warnings"].Values<string>().Any(w => w.Contains("evaluated as if it were enabled")));
			Assert.IsTrue(first["pipeline"][0].Value<bool>("candidate"));

			// Last (the default), "color info" runs first, so only the move changes.
			JObject last = Call(adminToken, "preview_filter", new { project, definition = candidate, mode = "pipeline", since = "all" });
			Assert.AreEqual(1, last.Value<int>("changedEvents"));
			Assert.IsNull(last["changes"]["filtersNoLongerApplied"]);
			Assert.IsNull(last["changes"]["colorChanged"]);

			// Replacing the existing filter with a version that colors nothing: two Info events lose their color.
			JObject replaced = Call(adminToken, "preview_filter", new { project, definition = new { name = "color info", enabled = true, conditionHandling = "All", conditions = new[] { C("EventType", "Equals", "Nope") }, actions = new[] { A("SetColor", "#111111") } }, mode = "pipeline", replaceFilterId = colorInfo, since = "all" });
			Assert.AreEqual(2, replaced.Value<int>("changedEvents"));
			Assert.AreEqual(0, replaced.Value<int>("candidateMatched"));

			// An existing filter previewed in pipeline mode is compared with the pipeline without it.
			JObject existing = Call(adminToken, "preview_filter", new { project, filterId = colorInfo, mode = "pipeline", since = "all" });
			Assert.AreEqual(2, existing.Value<int>("changedEvents"));
			Assert.AreEqual(2, existing.Value<int>("candidateMatched"));

			StringAssert.Contains(CallError(adminToken, "preview_filter", new { project, filterId = colorInfo, position = "first" }), "pipeline");
		}
		#endregion

		#region Triage tools
		[TestMethod]
		public void TriageToolsMoveColorAndReadState()
		{
			string project = NewProject();
			Event[] events = ParityEvents();
			AddEvents(project, events);
			long[] ids = events.Select(e => e.EventId).ToArray();

			// Moving and coloring need events:write.
			StringAssert.Contains(CallError(writeToken, "move_events", new { project, eventIds = ids, folder = "/" }), "events:write");

			StringAssert.Contains(CallError(adminToken, "move_events", new { project, eventIds = ids, folder = "/Triage" }), "not found");
			JObject moved = Call(adminToken, "move_events", new { project, eventIds = new[] { ids[0], ids[1], 999999 }, folder = "/Triage", createFolder = true });
			Assert.AreEqual(2, moved.Value<int>("moved"));
			Assert.IsTrue(moved.Value<bool>("folderCreated"));
			Assert.AreEqual("/Triage", moved.Value<string>("folderPath"));
			CollectionAssert.AreEqual(new long[] { 999999 }, moved["notFound"].Values<long>().ToArray());
			JObject again = Call(adminToken, "move_events", new { project, eventIds = new[] { ids[0] }, folder = moved.Value<int>("folderId") });
			Assert.AreEqual(0, again.Value<int>("moved"));
			Assert.AreEqual(1, again.Value<int>("alreadyInFolder"));

			JObject colored = Call(adminToken, "set_events_color", new { project, eventIds = ids, color = "#ff8800" });
			Assert.AreEqual(5, colored.Value<int>("colored"));
			Assert.AreEqual("#FF8800", colored.Value<string>("color"));
			Assert.AreEqual(5, Call(adminToken, "set_events_color", new { project, eventIds = ids, color = "FF8800" }).Value<int>("alreadyThatColor"));
			StringAssert.Contains(CallError(adminToken, "set_events_color", new { project, eventIds = ids, color = "orange" }), "color");

			// Read state is personal: another user with access is not affected.
			User other = new User("mcptriage", "pw", null, false);
			Settings.data.TryAddUser(other);
			other.InitializeUserId();
			other.AllowProject(project);
			using (DB db = new DB(project))
				db.AddReadState(other.UserId, ids);
			JObject unread = Call(readerTokenFor(project), "set_read_state", new { project, read = false, folder = "/Triage" });
			Assert.AreEqual(2, unread.Value<int>("updated"));
			JObject read = Call(adminToken, "set_read_state", new { project, read = true, eventIds = ids });
			Assert.AreEqual(5, read.Value<int>("updated"));
			using (DB db = new DB(project))
			{
				Assert.IsTrue(ids.All(id => db.ExistsReadState(admin.UserId, id)));
				Assert.IsTrue(ids.All(id => db.ExistsReadState(other.UserId, id)), "Another user's read state must not change.");
			}
			Call(adminToken, "set_read_state", new { project, read = false, folder = "/", includeSubfolders = true });
			using (DB db = new DB(project))
			{
				Assert.IsFalse(ids.Any(id => db.ExistsReadState(admin.UserId, id)));
				Assert.IsTrue(ids.All(id => db.ExistsReadState(other.UserId, id)), "Another user's read state must not change.");
			}
			StringAssert.Contains(CallError(adminToken, "set_read_state", new { project, read = true }), "exactly one");

			// list_folders can list a subtree.
			JObject sub = Call(adminToken, "list_folders", new { project, folder = "/Triage" });
			Assert.AreEqual("/Triage", sub["folders"].Single().Value<string>("path"));
			Assert.IsFalse(sub.Value<bool>("truncated"));
		}
		/// <summary>
		/// Creates an events:read token for a new user who has read-only access to the project.
		/// </summary>
		private static string readerTokenFor(string project)
		{
			User u = new User("mcpro" + Interlocked.Increment(ref projectCounter), "pw", null, false);
			Settings.data.TryAddUser(u);
			u.InitializeUserId();
			u.AllowProjectReadOnly(project);
			return NewToken(u, "read only", null);
		}
		#endregion

		#region TEST-7 Filter writes
		[TestMethod]
		public void UpdateIsAFullReplacementDiff()
		{
			string project = NewProject();
			JObject created = Call(writeToken, "create_filter", new
			{
				project,
				definition = Def("All", new[] { C("A", "Contains", "1"), C("B", "Contains", "2"), C("C", "Contains", "3") }, new[] { A("SetColor", "#111111"), A("MarkRead") })
			});
			JObject f = (JObject)created["filter"];
			Assert.AreEqual("mcpadmin (via token \"writer\")", f.Value<string>("updatedBy"));
			object newDef = new
			{
				name = "renamed",
				enabled = false,
				conditionHandling = "Any",
				conditions = new[] { C("C", "Equals", "3x"), C("A", "Contains", "1"), C("D", "EndsWith", "4", true, true) },
				actions = new[] { A("MarkRead"), A("SetColor", "#222222"), A("MoveTo", "/X/Y"), A("StopExecution") }
			};
			JObject updated = Call(writeToken, "update_filter", new { project, filterId = f.Value<int>("filterId"), etag = f.Value<string>("etag"), definition = newDef });
			JObject got = (JObject)Call(adminToken, "get_filter", new { project, filterId = f.Value<int>("filterId") })["filter"];
			Assert.AreEqual(updated["filter"].Value<string>("etag"), got.Value<string>("etag"));
			Assert.AreEqual("renamed", got.Value<string>("name"));
			CollectionAssert.AreEqual(new[] { "C", "A", "D" }, got["conditions"].Select(c => c.Value<string>("tagKey")).ToArray());
			Assert.IsTrue(got["conditions"][2].Value<bool>("regex") && got["conditions"][2].Value<bool>("invert"));
			CollectionAssert.AreEqual(new[] { "MarkRead", "SetColor", "MoveTo", "StopExecution" }, got["actions"].Select(a => a.Value<string>("type")).ToArray());
			Assert.AreEqual("#222222", got["actions"][1].Value<string>("argument"));

			// Shrink: conditions and actions are removed.
			JObject shrunk = Call(writeToken, "update_filter", new { project, filterId = f.Value<int>("filterId"), etag = got.Value<string>("etag"), definition = Def("All", new[] { C("Z", "Contains", "z") }, new[] { A("Delete") }) });
			Assert.AreEqual(1, ((JArray)shrunk["filter"]["conditions"]).Count);
			Assert.AreEqual(1, ((JArray)shrunk["filter"]["actions"]).Count);
			using (DB db = new DB(project))
			{
				FullFilter full = db.GetFilter(f.Value<int>("filterId"));
				Assert.AreEqual(1, full.conditions.Length);
				Assert.AreEqual(1, full.actions.Length);
			}
		}
		[TestMethod]
		public void StaleEtagIsAConflict()
		{
			string project = NewProject();
			JObject f = (JObject)Call(writeToken, "create_filter", new { project, definition = Def("All", new[] { C("A", "Contains", "1") }, new[] { A("MarkRead") }) })["filter"];
			// A human edits the filter in the web interface.
			using (DB db = new DB(project))
			{
				FullFilter full = db.GetFilter(f.Value<int>("filterId"));
				full.conditions[0].Query = "changed by a human";
				Assert.IsTrue(db.EditFilter(full, out string err), err);
			}
			string error = CallError(writeToken, "update_filter", new { project, filterId = f.Value<int>("filterId"), etag = f.Value<string>("etag"), definition = Def("All", new[] { C("A", "Contains", "2") }, new[] { A("MarkRead") }) });
			StringAssert.Contains(error, "Conflict");
			StringAssert.Contains(error, "changed by a human");
			StringAssert.Contains(CallError(writeToken, "delete_filter", new { project, filterId = f.Value<int>("filterId"), etag = f.Value<string>("etag") }), "Conflict");

			string etag = ((JObject)Call(adminToken, "get_filter", new { project, filterId = f.Value<int>("filterId") })["filter"]).Value<string>("etag");
			Assert.IsTrue(Call(writeToken, "delete_filter", new { project, filterId = f.Value<int>("filterId"), etag }).Value<bool>("deleted"));
			Assert.IsTrue(Call(writeToken, "delete_filter", new { project, filterId = f.Value<int>("filterId"), etag }).Value<bool>("alreadyDeleted"));
		}
		[TestMethod]
		public void FailedCreateLeavesNoPartialFilter()
		{
			string project = NewProject();
			string error = CallError(writeToken, "create_filter", new { project, definition = Def("All", new[] { C("A", "Contains", "1") }, new[] { A("MarkRead") }), position = new { before = 99999 } });
			StringAssert.Contains(error, "99999");
			using (DB db = new DB(project))
			{
				Assert.AreEqual(0, db.GetFilters(false).Count);
				Assert.AreEqual(0, db.FetchSql<long>(PetaPoco.Sql.Builder.Append("SELECT COUNT(*) FROM " + db.ProjectNameLower + ".FilterCondition")).Single());
				Assert.AreEqual(0, db.FetchSql<long>(PetaPoco.Sql.Builder.Append("SELECT COUNT(*) FROM " + db.ProjectNameLower + ".FilterAction")).Single());
			}
			// Validation lists every problem.
			error = CallError(writeToken, "create_filter", new { project, definition = Def("All", new[] { C("", "Contains", "1"), C("A", "Contains", "([", true) }, new[] { A("SetColor", "red"), A("MoveTo", "") }) });
			StringAssert.Contains(error, "tagKey");
			StringAssert.Contains(error, "regular expression");
			StringAssert.Contains(error, "hex color");
			StringAssert.Contains(error, "folder path");
		}
		[TestMethod]
		public void PositionsAndOrdering()
		{
			string project = NewProject();
			int a = Call(writeToken, "create_filter", new { project, definition = new { name = "a", enabled = true, conditionHandling = "Unconditional", conditions = new object[0], actions = new[] { A("MarkRead") } } })["filter"].Value<int>("filterId");
			int b = Call(writeToken, "create_filter", new { project, definition = new { name = "b", enabled = true, conditionHandling = "Unconditional", conditions = new object[0], actions = new[] { A("MarkRead") } } })["filter"].Value<int>("filterId");
			int c = Call(writeToken, "create_filter", new { project, definition = new { name = "c", enabled = true, conditionHandling = "Unconditional", conditions = new object[0], actions = new[] { A("MarkRead") } }, position = "first" })["filter"].Value<int>("filterId");
			int d = Call(writeToken, "create_filter", new { project, definition = new { name = "d", enabled = true, conditionHandling = "Unconditional", conditions = new object[0], actions = new[] { A("MarkRead") } }, position = new { after = c } })["filter"].Value<int>("filterId");
			int[] order = Call(adminToken, "list_filters", new { project })["filters"].Select(x => x.Value<int>("filterId")).ToArray();
			CollectionAssert.AreEqual(new[] { c, d, a, b }, order);
			StringAssert.Contains(CallError(writeToken, "create_filter", new { project, definition = new { name = "A", enabled = true, conditionHandling = "Unconditional", conditions = new object[0], actions = new object[0] } }), "already exists");
		}
		[TestMethod]
		public void RunFiltersRefusesDeleteAndCountsEffects()
		{
			string project = NewProject();
			AddEvents(project, ParityEvents());
			Call(writeToken, "create_filter", new { project, definition = new { name = "move info", enabled = true, conditionHandling = "All", conditions = new[] { C("EventType", "Equals", "Info") }, actions = new[] { A("MoveTo", "/Noise/Info"), A("SetColor", "#123456") } } });
			Call(writeToken, "create_filter", new { project, definition = new { name = "delete debug", enabled = true, conditionHandling = "All", conditions = new[] { C("EventType", "Equals", "Debug") }, actions = new[] { A("Delete") } } });
			Call(writeToken, "create_filter", new { project, definition = new { name = "disabled", enabled = false, conditionHandling = "Unconditional", conditions = new object[0], actions = new[] { A("Delete") } } });

			string error = CallError(writeToken, "run_filters", new { project });
			StringAssert.Contains(error, "delete debug");
			Assert.IsFalse(error.Contains("\"disabled\""), "Disabled filters would not run, so they are not named.");
			using (DB db = new DB(project))
				Assert.AreEqual(5, db.CountEvents(), "Nothing was run.");

			JObject run = Call(writeToken, "run_filters", new { project, allowDelete = true });
			Assert.AreEqual("completed", run.Value<string>("status"));
			Assert.AreEqual(5, run.Value<int>("evaluated"));
			Assert.AreEqual(3, run.Value<int>("matched"));
			Assert.AreEqual(2, run["effects"]["moved"].Single(m => m.Value<string>("path") == "/Noise/Info").Value<int>("count"));
			Assert.AreEqual(2, run["effects"].Value<int>("colored"));
			Assert.AreEqual(1, run["effects"].Value<int>("deleted"));
			CollectionAssert.AreEquivalent(new[] { "/Noise/Info" }, run["effects"]["foldersCreated"].Values<string>().ToArray());
			using (DB db = new DB(project))
				Assert.AreEqual(4, db.CountEvents());

			// Running again moves nothing new: the events are already where the filter puts them.
			JObject again = Call(writeToken, "run_filters", new { project, since = "1d", allowDelete = true });
			Assert.AreEqual(0, ((JArray)again["effects"]["moved"]).Count);
			Assert.AreEqual(0, again["effects"].Value<int>("colored"));

			// get_job returns the same result.
			JObject job = Call(writeToken, "get_job", new { jobId = run.Value<string>("jobId") });
			Assert.AreEqual("completed", job.Value<string>("status"));
			Assert.AreEqual(3, job.Value<int>("matched"));
			StringAssert.Contains(CallError(readerToken, "get_job", new { jobId = run.Value<string>("jobId") }), "filters:run");

			// FilterApplied records drive list_filters statistics.
			JObject moveInfo = (JObject)Call(adminToken, "list_filters", new { project })["filters"].First(f => f.Value<string>("name") == "move info");
			Assert.AreEqual(2, moveInfo.Value<int>("timesApplied"));
			Assert.IsNotNull(moveInfo["lastApplied"]);
			Assert.AreEqual("MoveTo /Noise/Info; SetColor #123456", moveInfo.Value<string>("actionsSummary"));
		}
		[TestMethod]
		public void AuditLogRecordsToolCalls()
		{
			string token = NewToken(admin, "audited", null);
			Call(token, "list_projects", new { });
			CallError(token, "list_folders", new { project = "nope" });
			using (GlobalDb db = new GlobalDb())
			{
				List<McpAuditRecord> records = db.GetMcpAuditRecords(null, null, null, null, null, 1000).Where(r => r.TokenName == "audited").ToList();
				Assert.AreEqual(2, records.Count);
				Assert.IsTrue(records.Any(r => r.ToolName == "list_projects" && r.Outcome == "ok" && r.UserName == "mcpadmin" && r.ResultSize > 0));
				Assert.IsTrue(records.Any(r => r.ToolName == "list_folders" && r.Outcome == "tool error" && r.Arguments.Contains("nope")));
			}
		}
		#endregion
	}
}

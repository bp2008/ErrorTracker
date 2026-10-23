namespace ErrorTrackerServer.Database.Global.Model
{
	/// <summary>
	/// One "tools/call" request received by the MCP endpoint.
	/// </summary>
	public class McpAuditRecord
	{
		/// <summary>
		/// Auto-incremented unique identifier for the record.
		/// </summary>
		public long McpAuditRecordId { get; set; }
		/// <summary>
		/// Time of the call in milliseconds since the unix epoch.
		/// </summary>
		public long Date { get; set; }
		/// <summary>
		/// Name of the user the token acts as.
		/// </summary>
		public string UserName { get; set; }
		/// <summary>
		/// ID of the API token which was used.
		/// </summary>
		public int ApiTokenId { get; set; }
		/// <summary>
		/// Name of the API token which was used.
		/// </summary>
		public string TokenName { get; set; }
		/// <summary>
		/// Source IP address of the request.
		/// </summary>
		public string IPAddress { get; set; }
		/// <summary>
		/// Name of the tool which was called.
		/// </summary>
		public string ToolName { get; set; }
		/// <summary>
		/// The "project" argument, if any.
		/// </summary>
		public string ProjectName { get; set; }
		/// <summary>
		/// The tool arguments as JSON, truncated to 2 KB.
		/// </summary>
		public string Arguments { get; set; }
		/// <summary>
		/// "ok", "tool error" or "protocol error".
		/// </summary>
		public string Outcome { get; set; }
		/// <summary>
		/// How long the call took, in milliseconds.
		/// </summary>
		public int DurationMs { get; set; }
		/// <summary>
		/// Size of the JSON-RPC response body, in characters.
		/// </summary>
		public int ResultSize { get; set; }
		/// <summary>
		/// Error message for failed calls, truncated.  Null for successful calls.
		/// </summary>
		public string Message { get; set; }
	}
}

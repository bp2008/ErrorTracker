using BPUtil;
using BPUtil.MVC;
using BPUtil.SimpleHttp;
using ErrorTrackerServer.Mcp;
using System;
using System.Collections.Generic;
using System.Net;

namespace ErrorTrackerServer.Controllers
{
	/// <summary>
	/// <para>The MCP (Model Context Protocol) endpoint for AI agents, served at "{appPath}mcp".</para>
	/// <para>Derives from <see cref="ETController"/> rather than <see cref="UserController"/> because it authenticates each request with an API token ("Authorization: Bearer ...") instead of a session ID.</para>
	/// </summary>
	public class Mcp : ETController
	{
		public ActionResult Index()
		{
			HttpProcessor p = Context.httpProcessor;
			string body = null;
			if (p.Request.HttpMethod == "POST")
			{
				if (p.Request.RequestBodyStream != null)
				{
					ByteUtil.ReadToEndResult read = ByteUtil.ReadToEndWithMaxLength(p.Request.RequestBodyStream, McpServer.MaxRequestBodyBytes);
					if (!read.EndOfStream)
						return Error("This server allows a maximum request body size of " + McpServer.MaxRequestBodyBytes + " bytes.", "413 Content Too Large");
					body = read.Data == null ? "" : ByteUtil.Utf8NoBOM.GetString(read.Data);
				}
				else
					body = "";
			}
			McpHttpRequest req = new McpHttpRequest()
			{
				HttpMethod = p.Request.HttpMethod,
				GetHeader = name => p.Request.Headers.Get(name),
				Body = body,
				RemoteIPAddress = p.RemoteIPAddressStr,
				IsHttps = p.secure_https,
				IsLoopback = p.RemoteIPAddress != null && IPAddress.IsLoopback(p.RemoteIPAddress)
			};
			McpHttpResponse res = McpServer.Handle(req);
			ActionResult result;
			if (res.Body == null)
				result = StatusCode(res.Status);
			else
			{
				result = new StringResult(res.Body, res.ContentType);
				result.ResponseStatus = res.Status;
			}
			foreach (KeyValuePair<string, string> header in res.Headers)
				result.headers.Add(header.Key, header.Value);
			result.headers.Add("Cache-Control", "no-store");
			return result;
		}
	}
}

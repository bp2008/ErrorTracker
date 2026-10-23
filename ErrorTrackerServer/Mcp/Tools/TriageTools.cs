using ErrorTrackerServer.Database.Global.Model;
using ErrorTrackerServer.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static ErrorTrackerServer.Mcp.Tools.McpOutputSchemas;

namespace ErrorTrackerServer.Mcp.Tools
{
	/// <summary>
	/// Shared code for the event triage tools.
	/// </summary>
	public static class TriageHelpers
	{
		/// <summary>
		/// Maximum number of event IDs per triage call.
		/// </summary>
		public const int MaxEventIds = 1000;
		public static JObject EventIdsSchema(string description)
		{
			return McpSchemas.Array(new JObject() { ["type"] = "integer", ["minimum"] = 1 }, description + " At most " + MaxEventIds + " per call.", 1, MaxEventIds);
		}
		/// <summary>
		/// Loads the folder and color of each existing event among the IDs.
		/// </summary>
		public static Dictionary<long, EventStateRow> LoadStates(McpCallContext ctx, DB db, long[] eventIds)
		{
			Dictionary<long, EventStateRow> states = null;
			McpCriteria.RunQuery(ctx, db, () => states = EventQueryService.GetEventStates(db, eventIds));
			return states;
		}
	}

	public class MoveEventsTool : McpTool
	{
		public override string Name { get { return "move_events"; } }
		public override string Title { get { return "Move events"; } }
		public override string RequiredScope { get { return ApiTokenScopes.EventsWrite; } }
		public override bool ReadOnlyHint { get { return false; } }
		public override string Description
		{
			get
			{
				return "Moves events to a folder. Folders are shared, so this changes what every user sees. "
					+ "The folder must exist unless createFolder is true (then a missing folder path is created; check the spelling). "
					+ "To move events automatically from now on, create a filter instead (create_filter).";
			}
		}
		public override JObject InputSchema
		{
			get
			{
				return McpSchemas.Object(new JObject()
				{
					["project"] = McpSchemas.Project(),
					["eventIds"] = TriageHelpers.EventIdsSchema("IDs of the events to move."),
					["folder"] = McpSchemas.Folder("Destination folder."),
					["createFolder"] = McpSchemas.Bool("If true and folder is a path that does not exist, create it. Default false.")
				}, "project", "eventIds", "folder");
			}
		}
		public override JObject OutputSchema
		{
			get
			{
				return Obj(new JObject()
				{
					["moved"] = T("integer"),
					["alreadyInFolder"] = T("integer"),
					["notFound"] = Arr(T("integer")),
					["folderId"] = T("integer"),
					["folderPath"] = T("string"),
					["folderCreated"] = T("boolean")
				}, "moved", "alreadyInFolder", "notFound", "folderId", "folderPath", "folderCreated");
			}
		}
		public override JObject Execute(McpCallContext ctx, JObject args)
		{
			Project p = ctx.RequireProject(args, true);
			long[] eventIds = McpArgs.GetLongArray(args, "eventIds").Distinct().ToArray();
			JToken folderArg = args["folder"];
			bool createFolder = McpArgs.GetBool(args, "createFolder", false);
			using (DB db = new DB(p.Name))
			{
				FolderStructure root = db.GetFolderStructure();
				FolderStructure target;
				bool created = false;
				if (createFolder && folderArg.Type == JTokenType.String && GetExisting(root, folderArg.Value<string>()) == null)
				{
					try
					{
						target = db.FolderResolvePath(folderArg.Value<string>().Trim());
					}
					catch (Exception ex)
					{
						throw new McpToolException("The folder path " + folderArg.ToString(Newtonsoft.Json.Formatting.None) + " could not be created: " + ex.Message);
					}
					if (target == null)
						throw new McpToolException("The folder path " + folderArg.ToString(Newtonsoft.Json.Formatting.None) + " could not be resolved. Use an absolute path such as \"/Noise/Bots\".");
					created = true;
				}
				else
					target = McpArgs.ResolveFolder(root, folderArg);

				Dictionary<long, EventStateRow> states = TriageHelpers.LoadStates(ctx, db, eventIds);
				long[] toMove = states.Values.Where(s => s.FolderId != target.FolderId).Select(s => s.EventId).ToArray();
				if (toMove.Length > 0)
					McpCriteria.RunQuery(ctx, db, () => db.MoveEvents(toMove, target.FolderId));
				return new JObject()
				{
					["moved"] = toMove.Length,
					["alreadyInFolder"] = states.Count - toMove.Length,
					["notFound"] = new JArray(eventIds.Where(id => !states.ContainsKey(id))),
					["folderId"] = target.FolderId,
					["folderPath"] = target.AbsolutePath,
					["folderCreated"] = created
				};
			}
		}
		private static FolderStructure GetExisting(FolderStructure root, string path)
		{
			try
			{
				return root.ResolvePath(path?.Trim(), null);
			}
			catch (Exception)
			{
				return null;
			}
		}
	}

	public class SetEventsColorTool : McpTool
	{
		public override string Name { get { return "set_events_color"; } }
		public override string Title { get { return "Set event color"; } }
		public override string RequiredScope { get { return ApiTokenScopes.EventsWrite; } }
		public override bool ReadOnlyHint { get { return false; } }
		public override string Description
		{
			get { return "Sets the color of events (the background of the event in the web interface's event list). Colors are shared, so this changes what every user sees. The default color is #EBEBEB."; }
		}
		public override JObject InputSchema
		{
			get
			{
				return McpSchemas.Object(new JObject()
				{
					["project"] = McpSchemas.Project(),
					["eventIds"] = TriageHelpers.EventIdsSchema("IDs of the events to color."),
					["color"] = new JObject() { ["type"] = "string", ["description"] = "Hex color \"#RRGGBB\".", ["pattern"] = "^#?[0-9a-fA-F]{6}$", ["maxLength"] = 7 }
				}, "project", "eventIds", "color");
			}
		}
		public override JObject OutputSchema
		{
			get
			{
				return Obj(new JObject()
				{
					["colored"] = T("integer"),
					["alreadyThatColor"] = T("integer"),
					["notFound"] = Arr(T("integer")),
					["color"] = T("string")
				}, "colored", "alreadyThatColor", "notFound", "color");
			}
		}
		public override JObject Execute(McpCallContext ctx, JObject args)
		{
			Project p = ctx.RequireProject(args, true);
			long[] eventIds = McpArgs.GetLongArray(args, "eventIds").Distinct().ToArray();
			string hex = McpArgs.GetString(args, "color").Trim().TrimStart('#');
			if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint color))
				throw new McpInvalidParamsException("color must be a hex color like \"#FF8800\".");
			using (DB db = new DB(p.Name))
			{
				Dictionary<long, EventStateRow> states = TriageHelpers.LoadStates(ctx, db, eventIds);
				long[] toColor = states.Values.Where(s => (s.Color & 0xFFFFFF) != color).Select(s => s.EventId).ToArray();
				if (toColor.Length > 0)
					McpCriteria.RunQuery(ctx, db, () => db.SetEventsColor(toColor, color));
				return new JObject()
				{
					["colored"] = toColor.Length,
					["alreadyThatColor"] = states.Count - toColor.Length,
					["notFound"] = new JArray(eventIds.Where(id => !states.ContainsKey(id))),
					["color"] = McpOutput.Color(color)
				};
			}
		}
	}

	public class SetReadStateTool : McpTool
	{
		public override string Name { get { return "set_read_state"; } }
		public override string Title { get { return "Mark events read or unread"; } }
		public override string RequiredScope { get { return ApiTokenScopes.EventsRead; } }
		public override bool ReadOnlyHint { get { return false; } }
		public override string Description
		{
			get
			{
				return "Marks events as read or unread for this token's user only. Other users' read state is not affected "
					+ "(unlike the MarkRead/MarkUnread filter actions, which affect every user). Give either eventIds, or a folder to mark every event in it.";
			}
		}
		public override JObject InputSchema
		{
			get
			{
				return McpSchemas.Object(new JObject()
				{
					["project"] = McpSchemas.Project(),
					["read"] = McpSchemas.Bool("true to mark as read, false to mark as unread."),
					["eventIds"] = TriageHelpers.EventIdsSchema("IDs of the events. Give either eventIds or folder."),
					["folder"] = McpSchemas.Folder("Mark every event in this folder. Give either eventIds or folder."),
					["includeSubfolders"] = McpSchemas.Bool("With folder: also mark events in all folders below it. Default false.")
				}, "project", "read");
			}
		}
		public override JObject OutputSchema
		{
			get
			{
				return Obj(new JObject()
				{
					["read"] = T("boolean"),
					["updated"] = T("integer"),
					["notFound"] = Arr(T("integer")),
					["folders"] = Arr(T("string"))
				}, "read", "updated");
			}
		}
		public override JObject Execute(McpCallContext ctx, JObject args)
		{
			Project p = ctx.RequireProject(args, false);
			bool read = McpArgs.GetBool(args, "read", true);
			bool hasIds = args["eventIds"] != null && args["eventIds"].Type != JTokenType.Null;
			bool hasFolder = args["folder"] != null && args["folder"].Type != JTokenType.Null;
			if (hasIds == hasFolder)
				throw new McpInvalidParamsException("Give exactly one of eventIds and folder.");
			int userId = ctx.User.UserId;
			JObject result = new JObject() { ["read"] = read };
			using (DB db = new DB(p.Name))
			{
				if (hasIds)
				{
					long[] eventIds = McpArgs.GetLongArray(args, "eventIds").Distinct().ToArray();
					Dictionary<long, EventStateRow> states = TriageHelpers.LoadStates(ctx, db, eventIds);
					long[] existing = states.Keys.ToArray();
					McpCriteria.RunQuery(ctx, db, () =>
					{
						if (read)
							db.AddReadState(userId, existing);
						else
							db.RemoveReadState(userId, existing);
					});
					result["updated"] = existing.Length;
					result["notFound"] = new JArray(eventIds.Where(id => !states.ContainsKey(id)));
				}
				else
				{
					FolderStructure folder = McpArgs.ResolveFolder(db.GetFolderStructure(), args["folder"]);
					int[] folderIds = McpArgs.GetFolderIds(folder, McpArgs.GetBool(args, "includeSubfolders", false));
					long updated = 0;
					McpCriteria.RunQuery(ctx, db, () =>
					{
						foreach (int folderId in folderIds)
						{
							updated += db.CountEventsInFolder(folderId);
							db.SetFolderReadState(userId, folderId, read);
						}
					});
					result["updated"] = updated;
					FolderStructure root = db.GetFolderStructure();
					result["folders"] = new JArray(folderIds.Select(id => McpOutput.FolderPath(root, id)));
				}
			}
			return result;
		}
	}
}

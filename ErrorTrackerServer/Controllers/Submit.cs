using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Code;
using ErrorTrackerServer.Database.Project.Model;
using ErrorTrackerServer.Filtering;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Controllers
{
	public class Submit : ETController
	{
		/// <summary>
		/// Returns 404 if "p" or "k" URL parameters are invalid.
		/// Returns 400 if a body has not been correctly POSTed.
		/// </summary>
		/// <returns></returns>
		public ActionResult Index()
		{
			if (Context.httpProcessor.http_method != "POST")
				return StatusCode("400 Bad Request");

			string projectName = Context.httpProcessor.GetParam("p");
			if (string.IsNullOrWhiteSpace(projectName) || !StringUtil.IsAlphaNumericOrUnderscore(projectName))
				return StatusCode("404 Not Found");

			string submitKey = Context.httpProcessor.GetParam("k");
			if (string.IsNullOrWhiteSpace(submitKey))
				return StatusCode("404 Not Found");

			Project p = Settings.data.GetProject(projectName);
			if (p == null)
				return StatusCode("404 Not Found");

			if (p.SubmitKey != submitKey)
				return StatusCode("404 Not Found");

			// After this point, the request is allowed.
			if (!ByteUtil.ReadToEndWithMaxLength(Context.httpProcessor.RequestBodyStream, 50 * 1024 * 1024, out byte[] data))
				return StatusCode("400 Request Body is Required");

			string str = ByteUtil.Utf8NoBOM.GetString(data);
			ErrorTrackerClient.Event clientEvent = JsonConvert.DeserializeObject<ErrorTrackerClient.Event>(str);
			if (string.IsNullOrEmpty(clientEvent?.Message))
				return StatusCode("400 Bad Request");

			// Convert from client library Event to database Event.
			// During this conversion, each tag key will be validated and changed if necessary.
			Event ev = new Event();
			ev.EventType = (EventType)clientEvent.EventType;
			ev.SubType = StringUtil.VisualizeSpecialCharacters(clientEvent.SubType);
			ev.Message = StringUtil.VisualizeSpecialCharacters(clientEvent.Message);
			ev.Date = clientEvent.Date;
			foreach (ErrorTrackerClient.ReadOnlyTag tag in clientEvent.GetAllTags())
				ev.SetTag(tag.Key, tag.Value);

			List<SubmitResult> results = new List<SubmitResult>();
			results.Add(InsertIntoProject(Context, p, ev));
			if (p.CloneTo != null)
			{
				foreach (string pName in p.CloneTo)
				{
					Project pCloneTarget = Settings.data.GetProject(pName);
					if (pCloneTarget != null && pCloneTarget != p)
						results.Add(InsertIntoProject(Context, pCloneTarget, ev));
				}
			}
			if (results.Any(r => r == SubmitResult.FatalError))
			{
				Emailer.SendError("A fatal error occurred while handling event submission.\r\nPOST Body:\r\n" + str, false);
				return this.Error("Fatal Error"); // Client will retry submission later.
			}
			else if (results.Any(r => r == SubmitResult.FilterError))
			{
				Emailer.SendError("An error occurred in FilterEngine while handling event submission.\r\nPOST Body:\r\n" + str, false);
				return this.Error("FilterEngine Error"); // Client will retry submission later.
			}
			else
			{
				return this.PlainText("OK");
			}
		}

		/// <summary>
		/// Clones the event and adds it (if necessary) to the project, then runs all enabled filters against it. Returns a string that is null if there was no error, or an error code such as "FILTER ERROR" if there was an error.
		/// </summary>
		/// <param name="p">Project to insert the event into.</param>
		/// <param name="eventOriginal">Event to clone and insert.  The event object is not changed by this method.</param>
		/// <returns>Returns a string that is null if there was no error, or an error code such as "FILTER ERROR" if there was an error.</returns>
		public static SubmitResult InsertIntoProject(RequestContext context, Project p, Event eventOriginal)
		{
			try
			{
				Event ev = JsonConvert.DeserializeObject<Event>(JsonConvert.SerializeObject(eventOriginal));
				ev.FolderId = 0;
				ev.Color = new Event().Color;
				using (FilterEngine fe = new FilterEngine(p.Name))
				{
					BasicEventTimer bet = fe.AddEventAndRunEnabledFilters(ev);
					if (Settings.data.verboseSubmitLogging)
						Util.SubmitLog(p.Name, "Event " + ev.EventId + " Submission Succeeded\r\n" + bet.ToString("\r\n"));
				}
				PushManager.Notify(p.Name, ev);
				return SubmitResult.OK;
			}
			catch (FilterException ex)
			{
				string timing = "\r\n" + ex.timer.ToString("\r\n");
				Util.SubmitLog(p.Name, "Event Submission Failed with FilterException" + timing + "\r\n" + ex.ToString());
				Logger.Debug(ex, "FilterEngine Error" + timing);
				Emailer.SendError(context, "FilterEngine Error" + timing, ex);
				return SubmitResult.FilterError;
			}
			catch (Exception ex)
			{
				Util.SubmitLog(p.Name, "Event Submission Failed with Exception\r\n" + ex.ToString());
				Logger.Debug(ex, "Unhandled exception thrown when inserting event into project \"" + p.Name + "\".");
				Emailer.SendError(context, "Unhandled exception thrown when inserting event into project \"" + p.Name + "\".", ex);
				return SubmitResult.FatalError;
			}
		}
	}
	public enum SubmitResult
	{
		OK,
		FilterError,
		FatalError
	}
}

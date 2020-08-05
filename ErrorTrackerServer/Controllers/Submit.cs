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
			byte[] data = Context.httpProcessor.PostBodyStream.ToArray();
			string str = ByteUtil.Utf8NoBOM.GetString(data);
			ErrorTrackerClient.Event clientEvent = JsonConvert.DeserializeObject<ErrorTrackerClient.Event>(str);
			if (string.IsNullOrEmpty(clientEvent?.Message))
				return StatusCode("400 Bad Request");

			// Convert from client library Event to database Event.
			// During this conversion, each tag key will be validated and changed if necessary.
			Event ev = new Event();
			ev.EventType = (EventType)clientEvent.EventType;
			ev.SubType = clientEvent.SubType;
			ev.Message = clientEvent.Message;
			ev.Date = clientEvent.Date;
			foreach (ErrorTrackerClient.ReadOnlyTag tag in clientEvent.GetAllTags())
				ev.SetTag(tag.Key, tag.Value);

			List<SubmitResult> results = new List<SubmitResult>();
			results.Add(InsertIntoProject(p, ev));
			if (p.CloneTo != null)
			{
				foreach (string pName in p.CloneTo)
				{
					Project pCloneTarget = Settings.data.GetProject(pName);
					if (pCloneTarget != null && pCloneTarget != p)
						results.Add(InsertIntoProject(pCloneTarget, ev));
				}
			}
			if (results.Any(r => r == SubmitResult.FatalError))
			{
				Emailer.SendError("A fatal error occurred while handling event submission.\r\nPOST Body:\r\n" + str, false);
				return this.Error("Fatal Error"); // Client will retry submission later.
			}
			else if (results.Any(r => r == SubmitResult.FilterError))
			{
				Emailer.SendError("A filter error occurred while handling event submission.\r\nPOST Body:\r\n" + str, false);
				return this.Error("Filter Error"); // Client will retry submission later.
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
		private SubmitResult InsertIntoProject(Project p, Event eventOriginal)
		{
			BasicEventTimer bet = new BasicEventTimer();
			try
			{
				Event ev = JsonConvert.DeserializeObject<Event>(JsonConvert.SerializeObject(eventOriginal));
				using (FilterEngine fe = new FilterEngine(p.Name))
				{
					bet.Start("Dupe Check");
					// If our response is not received by the client, they will most likely submit again, causing a duplicate to be received.
					// Check for duplicate submissions.
					List<Event> events = fe.db.GetEventsByDate(ev.Date, ev.Date);
					bool anyDupe = events.Any(existing =>
					{
						if (existing.Date == ev.Date
						&& existing.EventType == ev.EventType
						&& existing.SubType == ev.SubType
						&& existing.Message == ev.Message
						&& existing.GetTagCount() == ev.GetTagCount())
						{
							// All else is the same. Compare tags.
							List<Tag> existingTags = existing.GetAllTags();
							existingTags.Sort(CompareTags);

							List<Tag> newTags = ev.GetAllTags();
							newTags.Sort(CompareTags);

							for (int i = 0; i < existingTags.Count; i++)
								if (existingTags[i].Key != newTags[i].Key || existingTags[i].Value != newTags[i].Value)
									return false;

							return true;
						}
						return false;
					});

					// Skip adding the event if it is a duplicate.
					if (anyDupe)
						return SubmitResult.OK;

					bet.Start("Insert");
					// Add the event to the database.
					fe.db.AddEvent(ev);

					bet.Start("Filter");
					// Run Filters
					try
					{
						fe.RunEnabledFiltersAgainstEvent(ev);
					}
					catch (Exception ex)
					{
						bet.Stop();
						string timing = "\r\n" + bet.ToString("\r\n");
						Logger.Debug(ex, "Filter Error" + timing);
						Emailer.SendError(Context, "Filter Error" + timing, ex);
						return SubmitResult.FilterError;
					}
				}
				return SubmitResult.OK;
			}
			catch (Exception ex)
			{
				bet.Stop();
				string timing = "\r\n" + bet.ToString("\r\n");
				Logger.Debug(ex, "Unhandled exception thrown when inserting event into project \"" + p.Name + "\"" + timing + ".");
				Emailer.SendError(Context, "Unhandled exception thrown when inserting event into project \"" + p.Name + "\"" + timing + ".", ex);
				return SubmitResult.FatalError;
			}
		}

		private static int CompareTags(Tag a, Tag b)
		{
			int diff = a.Key.CompareTo(b.Key);
			if (diff == 0)
				diff = a.Value.CompareTo(b.Value);
			return diff;
		}
	}
	enum SubmitResult
	{
		OK,
		FilterError,
		FatalError
	}
}

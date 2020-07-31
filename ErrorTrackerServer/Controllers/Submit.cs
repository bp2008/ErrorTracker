﻿using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Code;
using ErrorTrackerServer.Database.Project.Model;
using ErrorTrackerServer.Filtering;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

			string error = InsertIntoProject(p, ev);
			if (error == null && p.CloneTo != null)
			{
				foreach (string pName in p.CloneTo)
				{
					Project pCloneTarget = Settings.data.GetProject(pName);
					if (pCloneTarget != null && pCloneTarget != p)
					{
						string cloneError = InsertIntoProject(pCloneTarget, ev);
						if (cloneError != null)
							error = cloneError;
					}
				}
			}
			if (error == null)
				return this.PlainText("OK");
			else
			{
				Emailer.SendError("An error \"" + error + "\" occurred while handling event submission.\r\nPOST Body:\r\n" + str, false);
				return this.PlainText(error);
			}
		}

		/// <summary>
		/// Clones the event and adds it (if necessary) to the project, then runs all enabled filters against it. Returns a string that is null if there was no error, or an error code such as "FILTER ERROR" if there was an error.
		/// </summary>
		/// <param name="p">Project to insert the event into.</param>
		/// <param name="eventOriginal">Event to clone and insert.  The event object is not changed by this method.</param>
		/// <returns>Returns a string that is null if there was no error, or an error code such as "FILTER ERROR" if there was an error.</returns>
		private string InsertIntoProject(Project p, Event eventOriginal)
		{
			try
			{
				Event ev = JsonConvert.DeserializeObject<Event>(JsonConvert.SerializeObject(eventOriginal));
				using (FilterEngine fe = new FilterEngine(p.Name))
				{
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
						return null;

					// Add the event to the database.
					fe.db.AddEvent(ev);

					// Run Filters
					try
					{
						fe.RunEnabledFiltersAgainstEvent(ev);
					}
					catch (Exception ex)
					{
						Logger.Debug(ex);
						Emailer.SendError(Context, "Filter Error", ex);
						return "FILTER ERROR";
					}
				}
				return null;
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
				Emailer.SendError(Context, "Unhandled exception thrown when inserting event into project \"" + p.Name + "\".", ex);
				return "UNHANDLED EXCEPTION";
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
}

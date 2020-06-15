using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Database.Model;
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

			// After this point, the request is trusted.
			byte[] data = Context.httpProcessor.PostBodyStream.ToArray();
			string str = ByteUtil.Utf8NoBOM.GetString(data);
			Event ev = JsonConvert.DeserializeObject<Event>(str);
			if (ev == null || (ev.Date == 0 && string.IsNullOrEmpty(ev.Message)))
				return StatusCode("400 Bad Request");

			using (FilterEngine fe = new FilterEngine(p.Name))
			{
				// If our response is not received by the client, they will most likely submit again.
				// Check for duplicate submissions.
				List<Event> events = fe.db.GetEventsByDate(ev.Date, ev.Date);
				bool anyDupe = events.Any(existing =>
				{
					if (existing.Date == ev.Date
					&& existing.EventType == ev.EventType
					&& existing.SubType == ev.SubType
					&& existing.Message == ev.Message
					&& existing.Tags.Count == ev.Tags.Count)
					{
						// All else is the same. Compare tags.
						foreach (Tag t in ev.Tags)
							t.ValidateKey();

						List<Tag> existingTags = new List<Tag>(existing.Tags);
						existingTags.Sort(CompareTags);

						List<Tag> newTags = new List<Tag>(ev.Tags);
						newTags.Sort(CompareTags);

						for (int i = 0; i < existingTags.Count; i++)
							if (existingTags[i].Key != newTags[i].Key || existingTags[i].Value != newTags[i].Value)
								return false;

						return true;
					}
					return false;
				});

				if (anyDupe)
					return this.PlainText("OK");

				fe.db.AddEvent(ev);

				try
				{
					fe.RunEnabledFiltersAgainstEvent(ev);
				}
				catch (Exception ex)
				{
					Logger.Debug(ex);
					return this.PlainText("FILTER ERROR");
				}
			}
			return this.PlainText("OK");
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

using BPUtil;
using ErrorTrackerServer.Database.Project.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	public static class PushManager
	{
		private static Thread pushThread;
		private static ConcurrentQueue<EventToNotifyAbout> newEvents = new ConcurrentQueue<EventToNotifyAbout>();
		static PushManager()
		{
			pushThread = new Thread(doPushBackgroundWork);
			pushThread.IsBackground = true;
			pushThread.Name = "Push Notifications";
			pushThread.Start();
		}
		/// <summary>
		/// Enqueues an event to be processed for push notifications.
		/// </summary>
		/// <param name="projectName"></param>
		/// <param name="ev"></param>
		public static void Notify(string projectName, Event ev)
		{
			newEvents.Enqueue(new EventToNotifyAbout(projectName, ev));
		}
		private static void doPushBackgroundWork()
		{
			try
			{
				WebPush.WebPushClient client = new WebPush.WebPushClient();
				while (true)
				{
					try
					{
						if (!newEvents.IsEmpty)
						{
							// Accumulate a list of events we want to notify each subscription about.
							Dictionary<string, List<EventToNotifyAbout>> accumulatedEvents = new Dictionary<string, List<EventToNotifyAbout>>();
							List<User> users = Settings.data.GetAllUsers();
							while (newEvents.TryDequeue(out EventToNotifyAbout en))
							{
								foreach (User u in users)
								{
									string[] subscriptionKeys = u.GetPushNotificationSubscriptions(en.projectName, en.ev.FolderId);
									foreach (string key in subscriptionKeys)
									{
										List<EventToNotifyAbout> events;
										if (!accumulatedEvents.TryGetValue(key, out events))
											accumulatedEvents[key] = events = new List<EventToNotifyAbout>();
										events.Add(en);
									}
								}
							}

							if (accumulatedEvents.Count > 0)
							{
								WebPush.VapidDetails vapidDetails = new WebPush.VapidDetails(GetVapidSubject(), Settings.data.vapidPublicKey, Settings.data.vapidPrivateKey);
								// Build and send one notification to each affected subscription.
								foreach (KeyValuePair<string, List<EventToNotifyAbout>> kvp in accumulatedEvents)
								{
									string subscriptionKey = kvp.Key;
									List<EventToNotifyAbout> events = kvp.Value;

									WebPush.PushSubscription subscription = null;
									try
									{
										dynamic dyn = JsonConvert.DeserializeObject(subscriptionKey);
										subscription = new WebPush.PushSubscription();
										subscription.Endpoint = dyn.endpoint;
										subscription.P256DH = dyn.keys.p256dh;
										subscription.Auth = dyn.keys.auth;
									}
									catch { }

									// For now, we'll just ignore any unparseable subscription keys.
									// I know this is asking for trouble later on, and I'm sorry for that.
									if (subscription != null)
									{
										StringBuilder sb = new StringBuilder();

										if (events.Count > 1)
											sb.AppendLine(events.Count + " new events:");
										foreach (EventToNotifyAbout en in events)
											sb.AppendLine(en.ev.EventType.ToString() + ": " + en.ev.SubType);

										PushMessage message = new PushMessage(Settings.data.systemName, sb.ToString());

										try
										{
											client.SendNotification(subscription, message.ToString(), vapidDetails);
										}
										catch (Exception ex)
										{
											Logger.Debug(ex, "Failed to send push notification.");
										}
									}
								}
							}
						}
						Thread.Sleep(1000);
					}
					catch (ThreadAbortException) { }
					catch (Exception ex)
					{
						Emailer.SendError(null, "Error in PushManager", ex);
					}
				}
			}
			catch (ThreadAbortException) { }
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}

		private static string GetVapidSubject()
		{
			string addressesStr = Settings.data.defaultErrorEmail;
			if (!string.IsNullOrWhiteSpace(addressesStr))
			{
				string[] allAddresses = addressesStr.Split(";,".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
				foreach (string address in allAddresses)
				{
					return "mailto:" + address;
				}
			}
			return "https://github.com/bp2008/ErrorTracker";
		}
	}
	class EventToNotifyAbout
	{
		public readonly string projectName;
		public readonly Event ev;
		public EventToNotifyAbout(string projectName, Event ev)
		{
			this.projectName = projectName;
			this.ev = ev;
		}
	}
	public class PushMessage
	{
		/// <summary>
		/// Title with a max length of 64 characters.
		/// </summary>
		public string title;
		/// <summary>
		/// Message with a max length of 128 characters.
		/// </summary>
		public string message;
		public PushMessage(string title, string message)
		{
			if (title == null)
				title = Settings.data.systemName;
			if (title == null)
				title = "Error Tracker";
			if (title.Length > 64)
				title = title.Substring(0, 64);

			if (message == null)
				throw new ArgumentNullException("message");
			if (message.Length > 128)
				message = message.Substring(0, 127) + "…";

			this.title = title;
			this.message = message;
		}
		/// <summary>
		/// Returns this object serialized as JSON.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}

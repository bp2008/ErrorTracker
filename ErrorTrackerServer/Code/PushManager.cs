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
							// Map subscriptionkey > event list
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
								HashSet<string> keysToDelete = new HashSet<string>();
								// Build and send one notification to each affected subscription.
								foreach (KeyValuePair<string, List<EventToNotifyAbout>> kvp in accumulatedEvents)
								{
									string subscriptionKey = kvp.Key;
									if (keysToDelete.Contains(subscriptionKey))
										continue;
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

										PushMessage message = new PushMessage(Settings.data.systemName, events.Count + " new events:");
										if (events.Count == 1)
										{
											EventToNotifyAbout en = events[0];
											message.message = en.ev.EventType.ToString() + ": " + en.ev.SubType;
											message.eventid = en.ev.EventId;
										}

										// If all events are in the same project, set message.project so that clicking the notification can open the correct project.
										string projectName = events[0].projectName;
										if (events.All(en => en.projectName == projectName))
										{
											message.project = projectName;

											// If all events are in the same folder, set message.folderid so that clicking the notification can open the correct folder.
											int folderId = events[0].ev.FolderId;
											if (events.All(en => en.ev.FolderId == folderId))
											{
												message.folderid = folderId;
											}
										}

										try
										{
											client.SendNotification(subscription, message.ToString(), vapidDetails);
										}
										catch (Exception ex)
										{
											if (ex is WebPush.WebPushException)
											{
												WebPush.WebPushException wpe = (WebPush.WebPushException)ex;
												if (wpe.Message != null && wpe.Message.Contains("Subscription no longer valid"))
												{
													keysToDelete.Add(subscriptionKey);
													Logger.Info("Deleting expired push subscription: " + subscriptionKey);
												}
												else
													Logger.Debug(ex, "Failed to send push notification.");
											}
											else
												Logger.Debug(ex, "Failed to send push notification.");
										}
									}
								}
								if (keysToDelete.Count > 0)
								{
									foreach (User u in users)
									{
										foreach (string key in keysToDelete)
											u.DeletePushNotificationSubscriptions(key);
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
		[JsonIgnore]
		private string _title;
		/// <summary>
		/// Title with automatically enforced max length of 64 characters.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "name consistency")]
		public string title
		{
			get
			{
				return _title;
			}
			set
			{
				string v = value;
				if (v == null)
					v = Settings.data.systemName;
				if (v == null)
					v = "Error Tracker";
				if (v.Length > 64)
					v = v.Substring(0, 64);
				_title = v;
			}
		}
		[JsonIgnore]
		private string _message;
		/// <summary>
		/// Message with automatically enforced max length of 128 characters.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "name consistency")]
		public string message
		{
			get
			{
				return _message;
			}
			set
			{
				string v = value;
				if (v == null)
					v = "";
				else if (v.Length > 128)
					v = value.Substring(0, 127) + "…";
				_message = v;
			}
		}
		/// <summary>
		/// Project Name. May be null/empty to indicate no specific project is associated with this message.
		/// </summary>
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string project;
		/// <summary>
		/// Folder ID. May be below 0 to indicate no specific folder is associated with this message.
		/// </summary>
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int? folderid = null;
		/// <summary>
		/// Event ID. May be below 0 to indicate no specific event is associated with this message.
		/// </summary>
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public long? eventid = null;
		public PushMessage(string title, string message)
		{
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

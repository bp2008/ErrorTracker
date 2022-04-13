using BPUtil;
using ErrorTrackerServer;
using ErrorTrackerServer.Database.Creation;
using ErrorTrackerServer.Database.Project.Model;
using ErrorTrackerServer.Filtering;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ErrorTrackerTests
{
	public class DbTestConfiguration
	{
		public string dbHost = "127.0.0.1";
		public int dbPort = 5432;
		public string dbAdminUser = "postgres";
		public string dbAdminPass = "";
		public string existingDbName = "postgres";
		public static DbTestConfiguration Load()
		{
			string path = Path.Combine(Directory.GetCurrentDirectory(), "ErrorTrackerTestConfig.json");
			if (File.Exists(path))
			{
				DbTestConfiguration cfg = JsonConvert.DeserializeObject<DbTestConfiguration>(File.ReadAllText(path, ByteUtil.Utf8NoBOM));
				Settings.data = new Settings();
				Settings.data.postgresHost = cfg.dbHost;
				Settings.data.postgresPort = cfg.dbPort;
				Settings.data.Save();
				return cfg;
			}
			else
			{
				File.WriteAllText(path, JsonConvert.SerializeObject(new DbTestConfiguration()), ByteUtil.Utf8NoBOM);
				throw new ApplicationException("Please set up test configuration at \"" + path + "\". You will not be warned again! LOL.");
			}
		}
	}
	[TestClass]
	public class TestDB
	{
		/// <summary>
		/// Performs extensive database testing.
		/// </summary>
		[TestMethod]
		public void TestDB1()
		{
			DbTestConfiguration cfg = DbTestConfiguration.Load();

			// Drop and recreate the testing database
			// Doing this also sets some static fields in DbCreation that ensures this app instance will continue to work with the testing database.
			DbCreation.TEST_ONLY_CreateTestingDatabase(cfg.dbAdminUser, cfg.dbAdminPass, cfg.existingDbName);

			using (DB db = new DB("TestProject1"))
			{
				// Add some events
				db.AddEvent(CreateTestEvent(1));

				db.RunInTransaction(() =>
				{
					db.AddEvent(CreateTestEvent(2));
				});

				db.RunInTransaction(() =>
				{
					db.AddEvent(CreateTestEvent(3));
					db.RunInTransaction(() =>
					{
						db.AddEvent(CreateTestEvent(4));
					});
					db.AddEvent(CreateTestEvent(5));
				});

				// Test transaction rollback
				try
				{
					db.RunInTransaction(() =>
					{
						db.AddEvent(CreateTestEvent(3));
						db.RunInTransaction(() =>
						{
							db.AddEvent(CreateTestEvent(4));
						});
						db.AddEvent(CreateTestEvent(5));
						throw new Exception("This exception should prevent events 3,4,5 from being added a second time.");
					});
					Assert.Fail("Expected exception");
				}
				catch (Exception ex)
				{
					if (!ex.ToString().Contains("This exception should prevent events 3,4,5 from being added a second time."))
						throw;
				}

				// Test transaction rollback
				try
				{
					db.RunInTransaction(() =>
					{
						db.AddEvent(CreateTestEvent(3));
						db.RunInTransaction(() =>
						{
							db.AddEvent(CreateTestEvent(4));
							throw new Exception("This exception should prevent events 3,4,5 from being added a second time.");
						});
						db.AddEvent(CreateTestEvent(5));
					});
					Assert.Fail("Expected exception");
				}
				catch (Exception ex)
				{
					if (!ex.ToString().Contains("This exception should prevent events 3,4,5 from being added a second time."))
						throw;
				}

				// Test transaction rollback
				try
				{
					bool didTryToAdd103 = false;
					bool didThrowAfterAdding102 = false;
					db.RunInTransaction(() =>
					{
						db.AddEvent(CreateTestEvent(101));
						try
						{
							db.RunInTransaction(() =>
							{
								db.AddEvent(CreateTestEvent(102));
								throw new Exception("This exception should prevent events 101,102,103 from being added.");
							});
						}
						catch
						{
							didThrowAfterAdding102 = true;
						}
						db.AddEvent(CreateTestEvent(103));
						didTryToAdd103 = true;
					});
					Assert.IsTrue(didThrowAfterAdding102);
					Assert.IsTrue(didTryToAdd103);
				}
				catch (Exception ex)
				{
					Assert.Fail("Here we expected the outer transaction to fail silently.  This is a behavior that is perhaps unique to the PetaPoco library and its transaction nesting implementation.  For now I think this is acceptable behavior, but I would have preferred that the outer transaction throw an exception when it is told to commit.  If the transaction actually added some events, we'll notice later and fail the test at that point. " + Environment.NewLine + Environment.NewLine + ex.ToString());
				}

				// Query all events to see if we get back what we originally added.
				List<Event> allAddedEvents = db.GetAllEventsNoTags(null);
				ValidateEvents(allAddedEvents, false);

				allAddedEvents = db.GetAllEventsDeferred(null).ToList();
				ValidateEvents(allAddedEvents, true);

				// Query events individually
				List<Event> fullEvents = allAddedEvents.Select(e => db.GetEvent(e.EventId)).ToList();
				ValidateEvents(fullEvents, true);

				// Query events in root folder (0)
				fullEvents = db.GetEventsInFolder(0);
				ValidateEvents(fullEvents, true);

				// Query all events by date
				fullEvents = db.GetEventsByDate(long.MinValue, long.MaxValue);
				ValidateEvents(fullEvents, true);

				// Query subset of events by date
				fullEvents = db.GetEventsByDate(2, 3);
				Assert.AreEqual(2, fullEvents.Count);
				Assert.AreEqual(2, fullEvents[0].Date);
				Assert.AreEqual(3, fullEvents[1].Date);

				// Delete the events
				db.DeleteEvents(new long[] { allAddedEvents[0].EventId });
				allAddedEvents = db.GetAllEventsNoTags(null).ToList();
				Assert.AreEqual(4, allAddedEvents.Count);
				Assert.AreEqual(CreateTestEvent(2).Message, allAddedEvents[0].Message);

				db.DeleteEvents(new long[] { allAddedEvents[0].EventId, allAddedEvents[1].EventId });
				allAddedEvents = db.GetAllEventsNoTags(null).ToList();
				Assert.AreEqual(2, allAddedEvents.Count);
				Assert.AreEqual(CreateTestEvent(4).Message, allAddedEvents[0].Message);

				db.DeleteEventsOlderThan(5);
				allAddedEvents = db.GetAllEventsNoTags(null).ToList();
				Assert.AreEqual(1, allAddedEvents.Count);
				Assert.AreEqual(5, allAddedEvents[0].Date);

				db.DeleteEventsOlderThan(10);
				allAddedEvents = db.GetAllEventsNoTags(null).ToList();
				Assert.AreEqual(0, allAddedEvents.Count);

				// Add multiple events
				db.AddEvents(new Event[] { CreateTestEvent(6), CreateTestEvent(7), CreateTestEvent(8) });
				allAddedEvents = db.GetAllEventsNoTags(null).ToList();
				Assert.AreEqual(3, allAddedEvents.Count);

				// Delete all events
				db.DeleteEventsOlderThan(long.MaxValue);
				allAddedEvents = db.GetAllEventsNoTags(null).ToList();
				Assert.AreEqual(0, allAddedEvents.Count);

				//////////////////
				// Test Filters //
				//////////////////
				Filter filter = new Filter();
				filter.ConditionHandling = ConditionHandling.Any;
				filter.Enabled = true;
				filter.Name = "Test Filter 1";
				FilterCondition[] conditions = new FilterCondition[]
				{
						new FilterCondition()
						{
							Enabled = true,
							Invert = false,
							Operator = FilterConditionOperator.Equals,
							Query = "This is test event #10.",
							Regex = false,
							TagKey = "Message"
						}
				};

				string errorMessage = null;
				Assert.IsTrue(db.AddFilter(filter, conditions, out errorMessage));
				Assert.IsNull(errorMessage);

				FilterAction filterAction = new FilterAction();
				filterAction.Enabled = true;
				filterAction.Operator = FilterActionType.Delete;
				filterAction.FilterId = filter.FilterId;
				Assert.IsTrue(db.AddFilterAction(filterAction, out errorMessage));
				Assert.IsNull(errorMessage);

				using (FilterEngine fe = new FilterEngine(db.ProjectName))
				{
					fe.AddEventAndRunEnabledFilters(CreateTestEvent(9));
					Assert.AreEqual(1, db.CountEvents());

					fe.AddEventAndRunEnabledFilters(CreateTestEvent(10));
					Assert.AreEqual(1, db.CountEvents());

					fe.AddEventAndRunEnabledFilters(CreateTestEvent(11));
					Assert.AreEqual(2, db.CountEvents());
				}
			}
		}
		private static Event CreateTestEvent(int eventNumber)
		{
			Event e = new Event();
			e.EventType = EventType.Debug;
			e.SubType = "TestDB1 Subtype";
			e.Message = "This is test event #" + eventNumber + ".";
			e.Color = (uint)eventNumber;
			e.Date = eventNumber;
			e.SetTag("Test Tag A", Math.Pow(2, eventNumber).ToString());
			e.SetTag("Test Tag B", "12345abc");
			e.ComputeHash();
			return e;
		}
		private void ValidateEvents(List<Event> events, bool expectTags)
		{
			Assert.AreEqual(5, events.Count);
			for (int i = 0; i < events.Count; i++)
			{
				int eventNumber = i + 1;
				Event expected = CreateTestEvent(eventNumber);
				Event actual = events[i];
				Assert.AreEqual("TestDB1 Subtype", actual.SubType);
				Assert.AreEqual((uint)eventNumber, actual.Color);
				Assert.AreEqual(eventNumber, actual.Date);
				Assert.AreEqual(eventNumber, actual.EventId);
				Assert.AreEqual(EventType.Debug, actual.EventType);
				Assert.AreEqual(0, actual.FolderId);
				Assert.IsFalse(string.IsNullOrEmpty(actual.HashValue));
				Assert.AreEqual(expected.Message, actual.Message);
				if (expectTags)
				{
					Assert.AreEqual(2, expected.GetTagCount());
					Assert.AreEqual(2, actual.GetTagCount());
					foreach (Tag tag in actual.GetAllTags())
					{
						Assert.AreEqual(0, tag.TagId, "A relatively unimportant implementation detail: when querying events, the TagId is not set within the event object.  This logic could be changed in the future which would require this assertion to be changed.");
						Assert.AreEqual(tag.EventId, actual.EventId);
						Assert.IsTrue(expected.TryGetTag(tag.Key, out string expectedA));
						Assert.IsTrue(actual.TryGetTag(tag.Key, out string actualA));
						Assert.AreEqual(tag.Value, actualA);
					}
				}
				else
				{
					Assert.AreEqual(2, expected.GetTagCount());
					Assert.AreEqual(0, actual.GetTagCount());
				}
			}
		}
	}
}

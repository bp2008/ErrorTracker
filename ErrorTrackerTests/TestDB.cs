using BPUtil;
using ErrorTrackerServer;
using ErrorTrackerServer.Database.Creation;
using ErrorTrackerServer.Database.Project.Model;
using ErrorTrackerServer.Database.Project.v2;
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
		private static Event CreateTestEvent(int eventNumber)
		{
			Event e = new Event();
			e.EventType = EventType.Debug;
			e.SubType = "TestDB1 Subtype";
			e.Message = "This is test event #" + eventNumber + ".";
			e.SetTag("Test Tag A", Math.Pow(2, eventNumber).ToString());
			e.SetTag("Test Tag B", "12345abc");
			e.ComputeHash();
			return e;
		}
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

				// Query all events to see if we get back what we originally added.
				List<Event> allAddedEvents = db.GetAllEventsNoTags(null);
				TestEvents(allAddedEvents, false);

				allAddedEvents = db.GetAllEventsNoTagsDeferred(null).ToList();
				TestEvents(allAddedEvents, false);

				List<Event> fullEvents = allAddedEvents.Select(e => db.GetEvent(e.EventId)).ToList();
				TestEvents(fullEvents, true);
			}
		}
		private void TestEvents(List<Event> events, bool expectTags)
		{
			Assert.AreEqual(5, events.Count);
			for (int i = 0; i < events.Count; i++)
			{
				Event expected = CreateTestEvent(i + 1);
				Event actual = events[i];
				Assert.AreEqual(expected.Message, actual.Message);
				if (expectTags)
				{
					Assert.AreEqual(2, expected.GetTagCount());
					Assert.AreEqual(2, actual.GetTagCount());
					foreach (Tag tag in actual.GetAllTags())
					{
						Assert.IsTrue(tag.TagId > 0);
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

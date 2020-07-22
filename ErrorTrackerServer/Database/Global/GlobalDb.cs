using BPUtil;
using ErrorTrackerServer.Database.Global.Model;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Database.Global
{
	public abstract class GlobalDb : IDisposable
	{
		#region Constructor / Fields
		/// <summary>
		/// Database version number useful for performing migrations. This number should only be incremented when migrations are in place to support upgrading all previously existing versions to this version.
		/// </summary>
		public const int dbVersion = 1;
		/// <summary>
		/// File name of the database.
		/// </summary>
		public readonly string DbFilename;
		/// <summary>
		/// Lazy-loaded database connection.
		/// </summary>
		private Lazy<SQLiteConnection> conn;
		private static bool initialized = false;
		private static object createMigrateLock = new object();
		/// <summary>
		/// Use within a "using" block to guarantee correct disposal.  Provides SQLite database access.  Not thread safe.  The DB connection will be lazy-loaded upon the first DB request.
		/// </summary>
		/// <param name="projectName">Project name, case insensitive. Please validate the project name before passing it in, as this class will create the database if it doesn't exist.</param>
		public GlobalDb()
		{
			DbFilename = "ErrorTrackerGlobalDB.s3db";
			conn = new Lazy<SQLiteConnection>(CreateDbConnection, false);
		}
		private SQLiteConnection CreateDbConnection()
		{
			SQLiteConnection c = new SQLiteConnection(Globals.WritableDirectoryBase + DbFilename, true);
			c.BusyTimeout = TimeSpan.FromSeconds(10);
			CreateOrMigrate(c);
			return c;
		}
		private void CreateOrMigrate(SQLiteConnection c)
		{
			if (!initialized)
			{
				lock (createMigrateLock)
				{
					if (!initialized)
					{
						initialized = true;

						c.CreateTable<DbVersion>();

						PerformDbMigrations(c);
					}
				}
			}
		}
		/// <summary>
		/// Performs Db Migrations. As this occurs during lazy connection loading, predefined API methods are unavailable here.
		/// </summary>
		/// <param name="c">The db connection.</param>
		private void PerformDbMigrations(SQLiteConnection c)
		{
			// Get current version
			DbVersion version = c.Query<DbVersion>("SELECT * From DbVersion").FirstOrDefault();
			if (version == null)
			{
				// This is a new DB. It will start at the latest version!
				version = new DbVersion() { CurrentVersion = dbVersion };
				c.Insert(version);
			}
		}
		/// <summary>
		/// True if the database is currently in a transaction.
		/// </summary>
		public bool IsInTransaction
		{
			get
			{
				return conn.IsValueCreated ? conn.Value.IsInTransaction : false;
			}
		}
		#endregion



		#region IDisposable
		private bool disposedValue;
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// dispose managed state (managed objects)
					if (conn.IsValueCreated)
						conn.Value.Dispose();
				}

				// free unmanaged resources (unmanaged objects) and override finalizer
				// set large fields to null
				disposedValue = true;
			}
		}

		// override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~DB()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}

using BPUtil;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Database.Creation
{
	public static class DbCreation
	{
		static DbCreation()
		{
		}
		/// <summary>
		/// Returns the connection string to connect to the ErrorTracker database.
		/// </summary>
		/// <returns></returns>
		public static string GetConnectionString()
		{
			if (string.IsNullOrEmpty(Settings.data.postgresPassword))
				throw new ApplicationException("PostgreSQL has not been initialized yet.");
			return InternalGetConnectionString(dbUser, Settings.data.GetPostgresPassword(), dbName);
		}
		private static string InternalGetConnectionString(string user, string pass, string db)
		{
			NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder();
			if (string.IsNullOrWhiteSpace(Settings.data.postgresHost))
				throw new ApplicationException("PostgreSQL hostname has not been configured in ErrorTracker settings.");
			builder["Host"] = Settings.data.postgresHost;
			if (Settings.data.postgresPort < 1 || Settings.data.postgresPort > 65535)
				throw new ApplicationException("PostgreSQL port number has not been configured in ErrorTracker settings.");
			builder["Port"] = Settings.data.postgresPort;
			builder["Username"] = user;
			builder["Password"] = pass;
			builder["Database"] = db;
			return builder.ConnectionString;
		}

		public static string dbName { get; private set; } = "ErrorTracker";
		public static string dbUser { get; private set; } = "errortracker";
		private static string SQL(string unprocessedSqlTemplate)
		{
			return unprocessedSqlTemplate
				.Replace("%DBNAME", dbName)
				.Replace("%DBUSER", dbUser);
		}

		#region Initial Db Creation
		/// <summary>
		/// Do not call this method except from a testing project.  Drops and recreates the "errortrackertest" user and "ErrorTrackerTest" database within PostgreSQL.  Creates a random password for the "errortrackertest" user and saves it in Settings.data.postgresPassword.
		/// </summary>
		/// <param name="dbAdminUser">PostgreSQL administrator username to use for initial setup.</param>
		/// <param name="dbAdminPass">PostgreSQL administrator password to use for initial setup.</param>
		/// <param name="existingDbName">Any existing PostgreSQL database name that is not the testing database. "postgres" should exist by default in new installations.</param>
		public static void TEST_ONLY_CreateTestingDatabase(string dbAdminUser, string dbAdminPass, string existingDbName)
		{
			dbName = "ErrorTrackerTest";
			dbUser = "errortrackertest";
			CreateInitialErrorTrackerDb(dbAdminUser, dbAdminPass, existingDbName);
		}
		/// <summary>
		/// Drops the error tracker database and role.
		/// </summary>
		/// <param name="dbAdminUser">PostgreSQL administrator username to use for initial setup.</param>
		/// <param name="dbAdminPass">PostgreSQL administrator password to use for initial setup.</param>
		/// <param name="existingDbName">Any existing PostgreSQL database name that is not the ErrorTracker database itself. "postgres" should exist by default in new installations.</param>
		private static void DropErrorTrackerDb(string dbAdminUser, string dbAdminPass, string existingDbName)
		{
			using (DbHelper db = new DbHelper(InternalGetConnectionString(dbAdminUser, dbAdminPass, existingDbName)))
			{
				db._ExecuteNonQuery("DROP DATABASE IF EXISTS \"" + dbName + "\" WITH (FORCE);");
				db._ExecuteNonQuery("DROP ROLE IF EXISTS " + dbUser);
			}
			Settings.data.postgresPassword = "";
		}
		/// <summary>
		/// Drops (if exists) and (re-)creates the "errortracker" user and "ErrorTracker" database within PostgreSQL.  Creates a random password for the "errortracker" user and saves it in Settings.data.postgresPassword.
		/// </summary>
		/// <param name="dbAdminUser">PostgreSQL administrator username to use for initial setup.</param>
		/// <param name="dbAdminPass">PostgreSQL administrator password to use for initial setup.</param>
		/// <param name="existingDbName">Any existing PostgreSQL database name that is not the ErrorTracker database itself. "postgres" should exist by default in new installations.</param>
		public static void CreateInitialErrorTrackerDb(string dbAdminUser, string dbAdminPass, string existingDbName)
		{
			DropErrorTrackerDb(dbAdminUser, dbAdminPass, existingDbName);

			string dbpw = Settings.data.GetPostgresPassword();
			if (string.IsNullOrEmpty(dbpw))
				dbpw = StringUtil.GetRandomAlphaNumericString(23);
			using (DbHelper db = new DbHelper(InternalGetConnectionString(dbAdminUser, dbAdminPass, existingDbName)))
			{
				try
				{
					db._ExecuteNonQuery(SQL(Properties.Resources.DbSetup_A_CreateRole).Replace("%DBPASSWORD", dbpw));
				}
				catch (Exception ex)
				{
					throw new ApplicationException("The '" + dbUser + "' role could not be created.  Delete the '" + dbUser + "' role within your PostgreSQL server, then try again.", ex);
				}

				try
				{
					db._ExecuteNonQuery(SQL(Properties.Resources.DbSetup_B_CreateDb));
					db._ExecuteNonQuery(SQL(Properties.Resources.DbSetup_C_CommentDb));
				}
				catch (Exception ex)
				{
					throw new ApplicationException("The '" + dbName + "' database could not be created.  Delete the '" + dbName + "' database within your PostgreSQL server, then try again.", ex);
				}
			}
			using (DbHelper db = new DbHelper(InternalGetConnectionString(dbAdminUser, dbAdminPass, dbName)))
			{
				db._ExecuteNonQuery(SQL(Properties.Resources.DbSetup_D_DropPublicSchema));
			}

			Settings.data.SetPostgresPassword(dbpw);
			Settings.data.Save();
		}
		#endregion
		#region Helpers
		/// <summary>
		/// Returns true if the given schema name exists in the database.
		/// </summary>
		/// <param name="schemaName">Case-insensitive schema name.</param>
		/// <param name="db">Database helper object</param>
		/// <returns></returns>
		private static bool SchemaExists(string schemaName, DbHelper db)
		{
			return db._ExecuteScalar<bool>("SELECT exists(select schema_name FROM information_schema.schemata WHERE schema_name = @schname);", new { schname = schemaName });
		}
		#endregion
		#region Create or Migrate Global Schema and Tables
		/// <summary>
		/// Creates or migrates the ErrorTrackerGlobal schema to the latest version.
		/// </summary>
		public static void CreateOrMigrateGlobalDb()
		{
			using (DbHelper db = new DbHelper())
			{
				db.RunInTransaction(() =>
				{
					if (!SchemaExists("errortrackerglobal", db))
					{
						// Create the ErrorTrackerGlobal database at version 2
						db._ExecuteNonQuery(SQL(Properties.Resources.GlobalSetup_2));
					}
					// Perform any necessary migrations to bring the schema up to date.
					PerformGlobalDbMigrations(db);
				});
			}
		}

		/// <summary>
		/// Perform any necessary migrations.  If the method does not throw an exception, it means the schema is up-to-date.
		/// </summary>
		/// <param name="db">Database helper object</param>
		private static void PerformGlobalDbMigrations(DbHelper db)
		{
			int dbVersion = db._ExecuteScalar<int>("SELECT CurrentVersion FROM ErrorTrackerGlobal.DbVersion LIMIT 1;");
			if (dbVersion < 2)
				throw new ApplicationException("ErrorTracker schema \"ErrorTrackerGlobal\" has unexpected DbVersion.CurrentVersion defined: " + dbVersion);

			if (dbVersion > 2)
				throw new ApplicationException("ErrorTracker schema \"ErrorTrackerGlobal\" has unexpected DbVersion.CurrentVersion defined: " + dbVersion);
		}
		#endregion
		#region Create or Migrate Project
		/// <summary>
		/// Creates or migrates the specified project to the latest DB version.
		/// </summary>
		/// <param name="projectName">Project name, case insensitive. Please validate the project name before passing it in, as this class will create the database if it doesn't exist.</param>
		public static void CreateOrMigrateProject(string projectName)
		{
			projectName = projectName.ToLower();
			using (DbHelper db = new DbHelper())
			{
				db.RunInTransaction(() =>
				{
					if (!SchemaExists(projectName, db))
					{
						// Create the project database at version 6
						db._ExecuteNonQuery(SQL(Properties.Resources.ProjectSetup_6_1_Tables).Replace("%PR", projectName));
					}
					// Perform any necessary migrations to bring the schema up to date.
					PerformProjectMigrations(projectName, db);
				});
			}
		}

		/// <summary>
		/// Perform any necessary migrations.  If the method does not throw an exception, it means the schema is up-to-date.
		/// </summary>
		/// <param name="db">Database helper object</param>
		private static void PerformProjectMigrations(string projectName, DbHelper db)
		{
			int dbVersion = db._ExecuteScalar<int>("SELECT CurrentVersion FROM " + projectName + ".DbVersion LIMIT 1;");
			if (dbVersion < 6)
				throw new ApplicationException("Project \"" + projectName + "\" has unexpected DbVersion.CurrentVersion defined: " + dbVersion);

			if (dbVersion > 8)
				throw new ApplicationException("Project \"" + projectName + "\" has unexpected DbVersion.CurrentVersion defined: " + dbVersion);

			if (dbVersion == 6)
			{
				// Migrate the project database to version 7
				db._ExecuteNonQuery(SQL(Properties.Resources.ProjectSetup_7_1).Replace("%PR", projectName));

				dbVersion = db._ExecuteScalar<int>("SELECT CurrentVersion FROM " + projectName + ".DbVersion LIMIT 1;");
				if (dbVersion != 7)
					throw new Exception("Project database version for \"" + projectName + "\" was " + dbVersion + " after performing migration from 6 to 7.");
			}

			if (dbVersion == 7)
			{
				// Migrate the project database to version 8
				db._ExecuteNonQuery(SQL(Properties.Resources.ProjectSetup_8_1).Replace("%PR", projectName));

				dbVersion = db._ExecuteScalar<int>("SELECT CurrentVersion FROM " + projectName + ".DbVersion LIMIT 1;");
				if (dbVersion != 8)
					throw new Exception("Project database version for \"" + projectName + "\" was " + dbVersion + " after performing migration from 7 to 8.");
			}
		}
		#endregion
	}
}

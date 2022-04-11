=============================
== ErrorTracker SQL Readme ==
=============================

======================
== ErrorTracker 1.x ==
======================

ErrorTracker 1.x used https://github.com/praeclarum/sqlite-net

Multiple separate database files were used, one for each Project, and one global database "ErrorTrackerGlobalDB.s3db" for storing login history.  Each database contains a "DbVersion" table with an integer "CurrentVersion" column and a single row indicating the version number of the database.

This database ORM library was determined to be unreliable after several databases were spontaneously overwritten with empty files.  Besides, large databases with 10,000+ events were getting rather slow to work with.

======================
== ErrorTracker 2.x ==
======================

ErrorTracker 2.x and newer uses PostgreSQL.

Rather than using multiple separate databases as was done in the past with SQLite databases, multiple PostgreSQL schemas are used.  One schema for each Project, and one global schema "ErrorTrackerGlobal" for storing login history.  Each schema contains a "DbVersion" table with an integer "CurrentVersion" column and a single row indicating the version number of the schema.

SQL source files are located in "SQL" directories, in subfolders named for the database version they belong to.  E.g. "SQL/6/", "SQL/7", etc.  It is the goal that as database schema changes are made, a sequentially numbered subfolder will be created to contain the migration script(s).

===================
== DB Migrations ==
===================

The first time any instance of ErrorTracker accesses a schema, it should check the version number if it is not current.  Then migrations should be performed sequentially until the version number is current.

New ErrorTracker releases requiring database structure updates shall:

* Add a subfolder named for the next version number.
* Add all necessary migration script files (*.psql) to this subfolder, named for their purpose and including their DbVersion number in the name.
* Add the update script files to the project's resources file (ErrorTrackerServer > Properties > Resources.resx) with FileType "Text" and Encoding UTF-8.
* In DbCreation.cs, code must be added to migrate from the previous DB version to the new DB version, which may involve custom C# code and/or executing the *.psql script files that were added, if any.  When finished, it must be ensured that the DbVersion.CurrentVersion number has been set appropriately.
* Ideally, the entire migration should occur within a transaction at the Serialized isolation level, such that it will be rolled back if there is any error.

When a schema is added to the ErrorTracker database, it is added at its initial version, then any available migrations are performed sequentially in order to bring the version number up-to-date.


==================
== Transactions ==
==================

Stored procedures could use savepoints, but should probably not start their own transactions.  Transactions should only be started and managed in application code, because I think PostgreSQL does not support nested transactions.
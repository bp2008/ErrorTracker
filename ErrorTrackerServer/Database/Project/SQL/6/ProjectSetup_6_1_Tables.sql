CREATE SCHEMA %PR
	AUTHORIZATION %DBUSER;





CREATE TABLE %PR.DbVersion
(
	CurrentVersion integer NOT NULL
);
ALTER TABLE %PR.DbVersion
	OWNER to %DBUSER;
INSERT INTO %PR.DbVersion (CurrentVersion) VALUES (6);

--------------------
-- Create Tables
--------------------

CREATE TABLE %PR.Event
(
	EventId bigint NOT NULL PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
	FolderId integer NOT NULL,
	EventType smallint NOT NULL,
	SubType varchar NOT NULL,
	Message varchar NOT NULL,
	Date bigint NOT NULL,
	Color integer NOT NULL,
	HashValue varchar NOT NULL
);
ALTER TABLE %PR.Event
	OWNER to %DBUSER;
CREATE INDEX Event_FolderId ON %PR.Event USING btree (FolderId ASC NULLS LAST);
CREATE INDEX Event_EventType ON %PR.Event USING btree (EventType ASC NULLS LAST);
CREATE INDEX Event_Date ON %PR.Event USING btree (Date ASC NULLS LAST);
CREATE INDEX Event_SubType ON %PR.Event USING btree (SubType ASC NULLS LAST);
CREATE INDEX Event_HashValue ON %PR.Event USING btree (HashValue ASC NULLS LAST);





CREATE TABLE %PR.Filter
(
	FilterId integer NOT NULL PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
	Enabled boolean NOT NULL DEFAULT false, 
	Regex boolean NOT NULL DEFAULT false,
	ConditionHandling smallint NOT NULL,
	Name text NOT NULL,
	MyOrder integer NOT NULL
);
ALTER TABLE %PR.Filter
	OWNER to %DBUSER;





CREATE TABLE %PR.FilterAction
(
	FilterActionId integer NOT NULL PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
	FilterId integer NOT NULL,
	Enabled boolean NOT NULL DEFAULT false,
	Operator smallint NOT NULL,
	Argument varchar NOT NULL
);
ALTER TABLE %PR.FilterAction
	OWNER to %DBUSER;





CREATE TABLE %PR.FilterCondition
(
	FilterConditionId integer NOT NULL PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
	FilterId integer NOT NULL,
	Enabled boolean NOT NULL DEFAULT false,
	TagKey varchar NOT NULL,
	Operator smallint NOT NULL,
	Query varchar NOT NULL,
	Regex boolean NOT NULL DEFAULT false,
	Invert boolean NOT NULL DEFAULT false
);
ALTER TABLE %PR.FilterCondition
	OWNER to %DBUSER;





CREATE TABLE %PR.Folder
(
	FolderId integer NOT NULL PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
	ParentFolderId integer NOT NULL,
	Name varchar NOT NULL
);
ALTER TABLE %PR.Folder
	OWNER to %DBUSER;





CREATE TABLE %PR.ReadState
(
	UserId integer NOT NULL,
	EventId bigint NOT NULL
);
ALTER TABLE %PR.ReadState
	OWNER to %DBUSER;
CREATE INDEX ReadState_EventId ON %PR.ReadState USING btree (EventId ASC NULLS LAST);
CREATE INDEX ReadState_UserId ON %PR.ReadState USING btree (UserId ASC NULLS LAST);





CREATE TABLE %PR.Tag
(
	TagId integer NOT NULL PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
	EventId bigint NOT NULL,
	Key varchar NOT NULL,
	Value varchar NULL
);
ALTER TABLE %PR.Tag
	OWNER to %DBUSER;
CREATE INDEX Tag_EventId ON %PR.Tag USING btree (EventId ASC NULLS LAST);
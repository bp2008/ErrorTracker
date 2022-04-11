CREATE SCHEMA ErrorTrackerGlobal
	AUTHORIZATION %DBUSER;





CREATE TABLE ErrorTrackerGlobal.DbVersion
(
	CurrentVersion integer NOT NULL
);
ALTER TABLE ErrorTrackerGlobal.DbVersion
	OWNER to %DBUSER;
INSERT INTO ErrorTrackerGlobal."DbVersion (CurrentVersion) VALUES (2);





CREATE TABLE ErrorTrackerGlobal.LoginRecord
(
	UserName varchar NOT NULL,
	IPAddress inet NOT NULL,
	SessionID varchar NOT NULL,
	Date bigint NOT NULL
);
ALTER TABLE ErrorTrackerGlobal.LoginRecord
	OWNER to %DBUSER;
CREATE INDEX LoginRecord_UserName ON ErrorTrackerGlobal.LoginRecord USING btree (UserName ASC NULLS LAST);
CREATE INDEX LoginRecord_Date ON ErrorTrackerGlobal.LoginRecord USING btree (Date ASC NULLS LAST);
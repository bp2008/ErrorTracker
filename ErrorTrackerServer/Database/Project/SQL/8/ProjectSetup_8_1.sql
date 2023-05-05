CREATE TABLE %PR.FilterApplied
(
	EventId bigint NOT NULL,
	FilterId integer NOT NULL,
	Date bigint NOT NULL
);
ALTER TABLE %PR.FilterApplied
	OWNER to %DBUSER;
CREATE INDEX FilterApplied_EventId ON %PR.FilterApplied USING btree (EventId ASC NULLS LAST);
CREATE INDEX FilterApplied_FilterId ON %PR.FilterApplied USING btree (FilterId ASC NULLS LAST);






    
UPDATE %PR.DbVersion SET CurrentVersion = 8;
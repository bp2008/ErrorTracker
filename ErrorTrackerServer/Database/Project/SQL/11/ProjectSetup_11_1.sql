-- DB version 11 supports the MCP (Model Context Protocol) endpoint.

-- Keyset paging of events newest-first ("ORDER BY Date DESC, EventId DESC").
CREATE INDEX Event_Date_EventId ON %PR.Event USING btree (Date DESC, EventId DESC);

-- The all-time first occurrence of each distinct error (MIN(Date) per HashValue), used to find errors that are new since a given time.
CREATE INDEX Event_HashValue_Date ON %PR.Event USING btree (HashValue ASC, Date ASC);

-- Latest application of each filter (MAX(Date) per FilterId), used to find filters that no longer match anything.
CREATE INDEX FilterApplied_FilterId_Date ON %PR.FilterApplied USING btree (FilterId ASC, Date ASC);

-- Remember who last changed each filter, so filters created or changed by an API token can be told apart from the ones people made.
ALTER TABLE %PR.Filter
	ADD COLUMN UpdatedBy varchar NULL;
ALTER TABLE %PR.Filter
	ADD COLUMN UpdatedDate bigint NULL;







UPDATE %PR.DbVersion SET CurrentVersion = 11;

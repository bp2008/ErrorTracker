-- DB version 3 adds personal API tokens and the audit log for the MCP (Model Context Protocol) endpoint.

CREATE TABLE ErrorTrackerGlobal.ApiToken
(
	ApiTokenId integer NOT NULL PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
	UserId integer NOT NULL,
	UserName varchar NOT NULL,
	Name varchar NOT NULL,
	TokenHash varchar NOT NULL,
	TokenPrefix varchar NOT NULL,
	Scopes varchar NOT NULL,
	Projects varchar NULL,
	Created bigint NOT NULL,
	Expires bigint NULL,
	LastUsed bigint NULL,
	LastUsedIP varchar NULL,
	Revoked boolean NOT NULL DEFAULT false
);
ALTER TABLE ErrorTrackerGlobal.ApiToken
	OWNER to %DBUSER;
CREATE UNIQUE INDEX ApiToken_TokenHash ON ErrorTrackerGlobal.ApiToken USING btree (TokenHash);
CREATE INDEX ApiToken_UserId ON ErrorTrackerGlobal.ApiToken USING btree (UserId ASC NULLS LAST);





CREATE TABLE ErrorTrackerGlobal.McpAuditRecord
(
	McpAuditRecordId bigint NOT NULL PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
	Date bigint NOT NULL,
	UserName varchar NOT NULL,
	ApiTokenId integer NOT NULL,
	TokenName varchar NOT NULL,
	IPAddress varchar NOT NULL,
	ToolName varchar NOT NULL,
	ProjectName varchar NULL,
	Arguments varchar NULL,
	Outcome varchar NOT NULL,
	DurationMs integer NOT NULL,
	ResultSize integer NOT NULL,
	Message varchar NULL
);
ALTER TABLE ErrorTrackerGlobal.McpAuditRecord
	OWNER to %DBUSER;
CREATE INDEX McpAuditRecord_Date ON ErrorTrackerGlobal.McpAuditRecord USING btree (Date ASC NULLS LAST);
CREATE INDEX McpAuditRecord_ApiTokenId ON ErrorTrackerGlobal.McpAuditRecord USING btree (ApiTokenId ASC NULLS LAST);






UPDATE ErrorTrackerGlobal.DbVersion SET CurrentVersion = 3;

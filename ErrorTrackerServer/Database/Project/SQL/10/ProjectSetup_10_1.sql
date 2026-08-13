-- DB version 10 adds the Event.MessageHtml flag, which indicates that Event.Message contains HTML markup
-- instead of plain text.  The HTML is stored exactly as it was submitted and is sanitized when it is served
-- to the web interface, so that changes to the server's sanitizer configuration apply to existing events.

ALTER TABLE %PR.Event
	ADD COLUMN MessageHtml boolean NOT NULL DEFAULT false;







UPDATE %PR.DbVersion SET CurrentVersion = 10;

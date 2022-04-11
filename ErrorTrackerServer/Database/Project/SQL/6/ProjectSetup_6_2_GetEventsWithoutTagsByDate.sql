DROP PROCEDURE IF EXISTS %PR."GetEvents";
CREATE PROCEDURE %PR."GetEvents" (
	IN oldest bigint, -- Pass null to disable this date boundary
	IN newest bigint, -- Pass null to disable this date boundary
	IN _folderid integer, -- If null, all folders will be searched
	IN customtagkey varchar, -- If null, no CTag will be added to the result set.
	IN includetags boolean, -- If true, a second table containing tags will be returned.
	OUT tags refcursor
)
LANGUAGE 'plpgsql'
AS $BODY$

BEGIN

	SELECT e.*, t.Value as CTag
	FROM %PR."Event" e
	LEFT JOIN %PR."Tag" t
		ON e.EventId = t.EventId
			AND (customtagkey IS NOT NULL AND t.Key = customtagkey)
	WHERE (oldest IS NULL OR e.Date >= oldest)
		  AND (newest IS NULL OR e.Date <= newest)
		  AND (_folderid IS NULL OR e.FolderId = _folderid);
		  
	IF includetags THEN
		OPEN tags FOR
			SELECT t.*
			FROM %PR.Tag t
			INNER JOIN %PR.Event e
				ON t.EventId = e.EventId
			WHERE (oldest IS NULL OR e.Date >= oldest)
				  AND (newest IS NULL OR e.Date <= newest)
				  AND (_folderid IS NULL OR e.FolderId = _folderid);
	END IF;
END;

$BODY$;
ALTER PROCEDURE %PR."GetEvents"(bigint, bigint, integer, varchar, boolean, refcursor)
    OWNER TO %DBUSER;

-- CALL testb."GetEvents"(0,1,NULL,NULL, false, null)
CREATE OR REPLACE FUNCTION %PR.EventTypeToString(EventType smallint)
    RETURNS character varying
    LANGUAGE 'sql'
    IMMUTABLE 
AS $BODY$
SELECT CASE
    WHEN EventType = 0 THEN 'Error'
    WHEN EventType = 1 THEN 'Info'
    WHEN EventType = 2 THEN 'Debug'    
    ELSE 'Unknown'    
    END;
$BODY$;

ALTER FUNCTION %PR.EventTypeToString(smallint)
    OWNER TO %DBUSER;









CREATE OR REPLACE FUNCTION %PR.ColorToString(color integer)
    RETURNS character varying
    LANGUAGE 'sql'
    IMMUTABLE

RETURN "substring"(lpad(to_hex(color), 8, '0'), 3);

ALTER FUNCTION %PR.ColorToString(integer)
    OWNER TO %DBUSER;







    


CREATE OR REPLACE FUNCTION %PR.DateToString(date bigint)
    RETURNS character varying
    LANGUAGE 'sql'
    IMMUTABLE
RETURN extract(year from TO_TIMESTAMP(date / 1000))
	|| '/'
	|| extract(month from TO_TIMESTAMP(date / 1000))
	|| '/'
	|| extract(day from TO_TIMESTAMP(date / 1000))
	|| TO_CHAR(TO_TIMESTAMP(date / 1000), ' HH12:MI:SS AM');

ALTER FUNCTION %PR.DateToString(bigint)
    OWNER TO %DBUSER;

-- This variant is slower
--CREATE OR REPLACE FUNCTION %PR.DateToString(date bigint)
--    RETURNS character varying
--    LANGUAGE 'sql'
--    IMMUTABLE
--RETURN TO_CHAR(TO_TIMESTAMP(date / 1000), 'YYYY/')
--		|| LTRIM(TO_CHAR(TO_TIMESTAMP(date / 1000), 'MM/'), '0')
--		|| LTRIM(TO_CHAR(TO_TIMESTAMP(date / 1000), 'DD '), '0')
--		|| TO_CHAR(TO_TIMESTAMP(date / 1000), ' HH12:MI:SS AM');
--
--ALTER FUNCTION %PR.DateToString(bigint)
--    OWNER TO %DBUSER;

-- This plpgsql variant is actually slowest despite doing less work
--CREATE OR REPLACE FUNCTION %PR.DateToString(IN datein bigint)
--    RETURNS character varying
--    LANGUAGE 'plpgsql'
--    IMMUTABLE
--AS $BODY$
--DECLARE dv TIMESTAMP;
--BEGIN
--	dv := TO_TIMESTAMP(datein / 1000);
--	RETURN extract(year from dv)
--		|| '/'
--		|| LTRIM(extract(month from dv)::text, '0')
--		|| '/'
--		|| LTRIM(extract(day from dv)::text, '0')
--		|| TO_CHAR(dv, ' HH12:MI:SS AM');
--END
--$BODY$;
--
--ALTER FUNCTION %PR.DateToString(bigint)
--    OWNER TO %DBUSER;







DROP INDEX IF EXISTS %PR.event_search_idx;
CREATE INDEX event_search_idx ON %PR.Event
USING GIN (to_tsvector('english', %PR.EventTypeToString(EventType) || ': ' || SubType || ': ' || %PR.ColorToString(Color) || ': ' || %PR.DateToString(Date) || ': ' || Message ));







DROP INDEX IF EXISTS %PR.tag_search_idx;
CREATE INDEX tag_search_idx ON %PR.Tag
USING GIN (to_tsvector('english', Value));






    
UPDATE %PR.DbVersion SET CurrentVersion = 7;
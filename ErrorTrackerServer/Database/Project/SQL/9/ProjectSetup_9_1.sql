
-- Everything added in DB version 9 is to support "SQL Full Text Search" on Filter, FilterCondition, and FilterAction.

CREATE OR REPLACE FUNCTION %PR.FilterActionOperatorToString(Operator smallint)
    RETURNS character varying
    LANGUAGE 'sql'
    IMMUTABLE 
AS $BODY$
SELECT CASE
    WHEN Operator = 0 THEN 'move event to'
    WHEN Operator = 1 THEN 'delete event'
    WHEN Operator = 2 THEN 'set event color'    
    WHEN Operator = 3 THEN 'stop execution against matched event'  
    WHEN Operator = 4 THEN 'mark read'  
    WHEN Operator = 5 THEN 'mark unread'  
    ELSE 'Unknown'    
    END;
$BODY$;

ALTER FUNCTION %PR.FilterActionOperatorToString(smallint)
    OWNER TO %DBUSER;








DROP INDEX IF EXISTS %PR.filter_search_idx;
CREATE INDEX filter_search_idx ON %PR.Filter
USING GIN (to_tsvector('english', Name));








DROP INDEX IF EXISTS %PR.filtercondition_search_idx;
CREATE INDEX filtercondition_search_idx ON %PR.FilterCondition
USING GIN (to_tsvector('english', TagKey || ': ' || Query));








DROP INDEX IF EXISTS %PR.filteraction_search_idx;
CREATE INDEX filteraction_search_idx ON %PR.FilterAction
USING GIN (to_tsvector('english', %PR.FilterActionOperatorToString(Operator) || ': ' || Argument));







    
UPDATE %PR.DbVersion SET CurrentVersion = 9;
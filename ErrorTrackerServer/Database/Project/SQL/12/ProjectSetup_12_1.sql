-- DB version 12 widens Tag.TagId from integer to bigint.  With 32-bit IDs, a project which receives floods of
-- events can run out of tag IDs (2,147,483,647), because deleted tags do not give their IDs back.
--
-- This rewrites the Tag table and rebuilds its indexes, which can take a long time on a large project.

ALTER TABLE %PR.Tag
	ALTER COLUMN TagId TYPE bigint;

-- PostgreSQL normally widens an identity column's sequence along with the column; make sure of it.
DO $$
BEGIN
	EXECUTE 'ALTER SEQUENCE ' || pg_get_serial_sequence('%PR.tag', 'tagid') || ' AS bigint';
END
$$;







UPDATE %PR.DbVersion SET CurrentVersion = 12;

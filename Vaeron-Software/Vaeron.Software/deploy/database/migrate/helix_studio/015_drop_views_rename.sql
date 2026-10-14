-- 拿掉全部 vw_*；資料表改用它裝的東西命名，不再帶 _management

DROP VIEW IF EXISTS dbo.vw_inspec_field;
DROP VIEW IF EXISTS dbo.vw_inspec_run;
DROP VIEW IF EXISTS dbo.vw_inspec_setting;
DROP VIEW IF EXISTS dbo.vw_key;
DROP VIEW IF EXISTS dbo.vw_rail;
DROP VIEW IF EXISTS dbo.vw_search;
DROP VIEW IF EXISTS dbo.vw_setting;
DROP VIEW IF EXISTS dbo.vw_shell_summary;
DROP VIEW IF EXISTS dbo.vw_tool;
DROP VIEW IF EXISTS dbo.vw_tool_change;
DROP VIEW IF EXISTS dbo.vw_tool_setting;

ALTER TABLE dbo.tool_management RENAME TO tool;
ALTER SEQUENCE dbo.tool_management_id_seq RENAME TO tool_id_seq;
ALTER TABLE dbo.tool RENAME CONSTRAINT tool_management_pkey TO tool_pkey;

ALTER TABLE dbo.key_management RENAME TO tool_key;
ALTER SEQUENCE dbo.key_management_id_seq RENAME TO tool_key_id_seq;
ALTER TABLE dbo.tool_key RENAME CONSTRAINT key_management_pkey TO tool_key_pkey;

ALTER TABLE dbo.search_management RENAME TO search;
ALTER SEQUENCE dbo.search_management_id_seq RENAME TO search_id_seq;
ALTER TABLE dbo.search RENAME CONSTRAINT search_management_pkey TO search_pkey;
ALTER TABLE dbo.search RENAME CONSTRAINT search_management_term_key TO search_term_key;

ALTER TABLE dbo.setting_management RENAME TO setting;
ALTER SEQUENCE dbo.setting_management_id_seq RENAME TO setting_id_seq;
ALTER TABLE dbo.setting RENAME CONSTRAINT setting_management_pkey TO setting_pkey;

ALTER TABLE dbo.inspec_management RENAME TO inspec;
ALTER SEQUENCE dbo.inspec_management_id_seq RENAME TO inspec_id_seq;
ALTER TABLE dbo.inspec RENAME CONSTRAINT inspec_management_pkey TO inspec_pkey;

COMMENT ON COLUMN dbo.tool_key.tool_no IS '哪一支工具，對到 tool.tool_no';

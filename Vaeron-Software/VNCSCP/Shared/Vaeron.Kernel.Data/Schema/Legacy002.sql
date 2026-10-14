-- 把「群組是獨立實體」的舊形狀對齊成「群組是 connection 上的一個文字欄位」。
-- 僅在 connection 仍帶 group_id 欄位時套用，且必須早於基準腳本。
-- 先清掉所有指向 connection 的檢視：SQLite 在既有檢視仍指向某表時不允許改動該表。

DROP VIEW IF EXISTS view_connections;
DROP VIEW IF EXISTS view_connection_groups;
DROP VIEW IF EXISTS view_event_log;
DROP VIEW IF EXISTS view_deployment_history;

DROP VIEW IF EXISTS Vw_ConnectionRecentEvent;
DROP VIEW IF EXISTS Vw_ConnectionGroup;
DROP VIEW IF EXISTS Vw_Connection;
DROP VIEW IF EXISTS Vw_EventLog;
DROP VIEW IF EXISTS Vw_DeploymentHistory;

CREATE TABLE connection_migrated (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    name              TEXT    NOT NULL DEFAULT '',
    ip                TEXT    NOT NULL,
    port              INTEGER NOT NULL,
    password_cipher   BLOB    NOT NULL,
    status            INTEGER NOT NULL DEFAULT 0,
    last_tested_at    TEXT    NULL,
    last_test_message TEXT    NOT NULL DEFAULT '',
    created_at        TEXT    NOT NULL DEFAULT (datetime('now')),
    is_deleted        INTEGER NOT NULL DEFAULT 0,
    group_name        TEXT    NULL
);

INSERT INTO connection_migrated (
    id, name, ip, port, password_cipher, status,
    last_tested_at, last_test_message, created_at, is_deleted, group_name)
SELECT
    c.id, c.name, c.ip, c.port, c.password_cipher, c.status,
    c.last_tested_at, c.last_test_message, c.created_at, c.is_deleted,
    (SELECT g.name FROM connection_group g WHERE g.id = c.group_id)
FROM connection c;

DROP TABLE connection;

ALTER TABLE connection_migrated RENAME TO connection;

DROP TABLE IF EXISTS connection_group;
DROP TABLE IF EXISTS connection_management_schema_version;
DROP TABLE IF EXISTS event_log_schema_version;
DROP TABLE IF EXISTS remote_deployment_schema_version;

-- 主題萬用表：一個主題一張表，新增需求時加欄位，不另立表。
-- 前端一律經 Vw_* 映照，分類、文字、衍生欄位一律由這裡承載。

CREATE TABLE IF NOT EXISTS connection (
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

CREATE TABLE IF NOT EXISTS event_log (
    id                    INTEGER PRIMARY KEY AUTOINCREMENT,
    occurred_utc_ticks    INTEGER NOT NULL,
    level                 INTEGER NOT NULL,
    source                TEXT    NOT NULL DEFAULT '',
    message               TEXT    NOT NULL DEFAULT '',
    detail                TEXT    NOT NULL DEFAULT '',
    related_connection_id INTEGER NULL
);

CREATE INDEX IF NOT EXISTS ix_event_log_occurred_utc_ticks    ON event_log (occurred_utc_ticks);
CREATE INDEX IF NOT EXISTS ix_event_log_related_connection_id ON event_log (related_connection_id);

CREATE TABLE IF NOT EXISTS deployment_history (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    deployed_ticks INTEGER NOT NULL,
    connection_id  INTEGER NOT NULL,
    target_name    TEXT    NOT NULL DEFAULT '',
    target_ip      TEXT    NOT NULL DEFAULT '',
    local_path     TEXT    NOT NULL DEFAULT '',
    remote_path    TEXT    NOT NULL DEFAULT '',
    success        INTEGER NOT NULL DEFAULT 0,
    message        TEXT    NOT NULL DEFAULT '',
    operated_by    TEXT    NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS ix_deployment_history_deployed_ticks ON deployment_history (deployed_ticks);

-- View 每次啟動重建，定義以本檔為唯一來源。

DROP VIEW IF EXISTS Vw_ConnectionRecentEvent;
DROP VIEW IF EXISTS Vw_ConnectionGroup;
DROP VIEW IF EXISTS Vw_Connection;
DROP VIEW IF EXISTS Vw_EventLog;
DROP VIEW IF EXISTS Vw_DeploymentHistory;

CREATE VIEW Vw_Connection AS
SELECT
    c.id,
    c.name,
    c.ip,
    c.port,
    c.password_cipher,
    c.status,
    CASE c.status
        WHEN 0 THEN '尚未測試'
        WHEN 1 THEN '連線正常'
        WHEN 2 THEN '密碼錯誤'
        WHEN 3 THEN '暫時被鎖定'
        WHEN 4 THEN '無法連線'
        ELSE '未知狀態'
    END                                                          AS status_text,
    CASE WHEN c.status IN (2, 3, 4) THEN 1 ELSE 0 END            AS is_abnormal,
    c.last_tested_at,
    COALESCE(SUBSTR(c.last_tested_at, 1, 19), '—')               AS last_tested_display,
    c.last_test_message,
    c.created_at,
    c.group_name,
    COALESCE(c.group_name, '未分組')                              AS group_display_name
FROM connection c
WHERE c.is_deleted = 0;

CREATE VIEW Vw_ConnectionGroup AS
SELECT DISTINCT c.group_name AS name
FROM connection c
WHERE c.is_deleted = 0
  AND c.group_name IS NOT NULL
  AND TRIM(c.group_name) <> ''
ORDER BY c.group_name;

CREATE VIEW Vw_EventLog AS
SELECT
    e.id,
    e.occurred_utc_ticks,
    e.level,
    CASE e.level
        WHEN 0 THEN '一般'
        WHEN 1 THEN '警告'
        WHEN 2 THEN '錯誤'
        ELSE '未知'
    END                                                          AS level_text,
    strftime('%m/%d %H:%M',
             (e.occurred_utc_ticks - 621355968000000000) / 10000000,
             'unixepoch', 'localtime')                           AS occurred_display,
    e.source,
    e.message,
    e.detail,
    CASE WHEN LENGTH(TRIM(e.detail)) > 0 THEN 1 ELSE 0 END       AS has_detail,
    e.related_connection_id
FROM event_log e;

CREATE VIEW Vw_ConnectionRecentEvent AS
SELECT id, occurred_utc_ticks, level, level_text, occurred_display,
       source, message, detail, has_detail, related_connection_id
FROM (
    SELECT v.*,
           ROW_NUMBER() OVER (PARTITION BY v.related_connection_id
                              ORDER BY v.occurred_utc_ticks DESC) AS rn
    FROM Vw_EventLog v
    WHERE v.related_connection_id IS NOT NULL
)
WHERE rn <= 5;

CREATE VIEW Vw_DeploymentHistory AS
SELECT
    d.id,
    d.deployed_ticks,
    strftime('%Y-%m-%d %H:%M',
             (d.deployed_ticks - 621355968000000000) / 10000000,
             'unixepoch', 'localtime')                           AS deployed_display,
    d.connection_id,
    d.target_name,
    d.target_ip,
    d.local_path,
    d.remote_path,
    d.success,
    CASE WHEN d.success = 1 THEN '成功' ELSE '失敗' END           AS success_text,
    d.message,
    d.operated_by
FROM deployment_history d;

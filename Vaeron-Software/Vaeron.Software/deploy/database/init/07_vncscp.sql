-- VNCSCP：設定、機台、每一次送出與測試；一列是什麼由 type 決定。藍圖 .claude/blueprint/藍圖_VNCSCP.md

CREATE TABLE dbo.vncscp (
    id           serial PRIMARY KEY,
    type         character varying(7) NOT NULL,
    setting_key  character varying(6),
    path         character varying(32767),
    name         character varying(50),
    ip           character varying(45),
    port         integer,
    remote_path  character varying(32767),
    machine_id   integer,
    batch        uuid,
    file_count   integer,
    total_bytes  bigint,
    sent_bytes   bigint,
    local_mtime  timestamp with time zone,
    started_at   timestamp with time zone,
    connected_ms integer,
    finished_at  timestamp with time zone,
    cancelled_at timestamp with time zone,
    note         text,
    removed_at   timestamp with time zone,
    created_at   timestamp with time zone NOT NULL DEFAULT now(),
    updated_at   timestamp with time zone NOT NULL DEFAULT now()
);

-- 每一項設定只有一列；同一個 IP:Port 只有一台還在的機台
CREATE UNIQUE INDEX vncscp_setting ON dbo.vncscp (setting_key) WHERE type = 'setting';
CREATE UNIQUE INDEX vncscp_machine ON dbo.vncscp (ip, port) WHERE type = 'machine' AND removed_at IS NULL;

COMMENT ON TABLE dbo.vncscp IS 'VNCSCP：setting 一項設定、machine 一台機台（移除寫 removed_at）、send 送一台的一次、test 測一台的一次；VNC 密碼不在這裡，鎖在平台保險箱';
COMMENT ON COLUMN dbo.vncscp.id IS '流水號';
COMMENT ON COLUMN dbo.vncscp.type IS 'setting＝一項設定；machine＝一台機台；send＝送一台的一次；test＝測一台的一次';
COMMENT ON COLUMN dbo.vncscp.setting_key IS '[setting] source（上次選的來源資料夾）';
COMMENT ON COLUMN dbo.vncscp.path IS '[setting] 路徑類的值；還沒選是空的';
COMMENT ON COLUMN dbo.vncscp.name IS '[machine] 機台名稱，可以空著；新增機台的輸入框 MaxLength 也是 50';
COMMENT ON COLUMN dbo.vncscp.ip IS '[machine] IP；IPv6 文字最長 45';
COMMENT ON COLUMN dbo.vncscp.port IS '[machine] UltraVNC 的 Port';
COMMENT ON COLUMN dbo.vncscp.remote_path IS '[machine] 送到機台上的哪個資料夾；[send] 這一次送到哪裡';
COMMENT ON COLUMN dbo.vncscp.machine_id IS '[send][test] 哪一台（machine 那一列的 id）';
COMMENT ON COLUMN dbo.vncscp.batch IS '[send][test] 同一次按「送到勾選的」或「測試連線」的那一批';
COMMENT ON COLUMN dbo.vncscp.file_count IS '[send] 這一次送幾個檔';
COMMENT ON COLUMN dbo.vncscp.total_bytes IS '[send] 這一次要送的總位元組';
COMMENT ON COLUMN dbo.vncscp.sent_bytes IS '[send] 實際送到的位元組';
COMMENT ON COLUMN dbo.vncscp.local_mtime IS '[send] 這一次送的檔裡最新的修改時間，也就是機台收到的那一份';
COMMENT ON COLUMN dbo.vncscp.started_at IS '[send][test] 開始連線的時刻';
COMMENT ON COLUMN dbo.vncscp.connected_ms IS '[send][test] 從開始到登入成功花了幾毫秒；沒登入成功是空的';
COMMENT ON COLUMN dbo.vncscp.finished_at IS '[send][test] 結束的時刻：送完、測完、失敗、取消都是';
COMMENT ON COLUMN dbo.vncscp.cancelled_at IS '[send][test] 使用者按取消的時刻';
COMMENT ON COLUMN dbo.vncscp.note IS '[send][test] 沒成功的原因，照實寫；成功是空的';
COMMENT ON COLUMN dbo.vncscp.removed_at IS '[machine] 移除的時刻；移除是狀態，列不真的刪';
COMMENT ON COLUMN dbo.vncscp.created_at IS '這一列建立的時刻';
COMMENT ON COLUMN dbo.vncscp.updated_at IS '這一列最後修改的時刻';

-- 設定的每一項：來源資料夾還沒選
INSERT INTO dbo.vncscp (type, setting_key, path) VALUES ('setting', 'source', NULL);

-- VNCSCP 主畫面：setting 一列（來源資料夾），machine 一台還在的機台一列（機台那份、上次送達、上次回應、上次沒成功的原因、在不在最後一批送出裡），event 一次送或測一列（新的在前）
CREATE VIEW dbo.vw_vncscp AS
WITH machine AS (
    SELECT m.* FROM dbo.vncscp AS m WHERE m.type = 'machine' AND m.removed_at IS NULL),
run AS (
    SELECT r.*,
           coalesce(nullif(m.name, ''), m.ip || ':' || m.port) AS machine_name,
           m.ip || ':' || m.port AS machine_address,
           (extract(epoch FROM (r.finished_at - r.started_at)) * 1000)::bigint AS took_ms
      FROM dbo.vncscp AS r
      JOIN dbo.vncscp AS m ON m.type = 'machine' AND m.id = r.machine_id
     WHERE r.type IN ('send', 'test')),
received AS (
    SELECT DISTINCT ON (r.machine_id) r.machine_id, r.local_mtime, r.finished_at
      FROM run AS r
      JOIN machine AS m ON m.id = r.machine_id
     WHERE r.type = 'send' AND r.finished_at IS NOT NULL AND r.note IS NULL AND r.cancelled_at IS NULL
       AND r.remote_path = m.remote_path
     ORDER BY r.machine_id, r.finished_at DESC, r.id DESC),
pinged AS (
    SELECT DISTINCT ON (r.machine_id) r.machine_id, r.connected_ms, r.started_at
      FROM run AS r
     WHERE r.connected_ms IS NOT NULL
     ORDER BY r.machine_id, r.started_at DESC, r.id DESC),
latest AS (
    SELECT DISTINCT ON (r.machine_id) r.machine_id, r.note, r.finished_at, r.cancelled_at
      FROM run AS r
     WHERE r.finished_at IS NOT NULL
     ORDER BY r.machine_id, r.finished_at DESC, r.id DESC),
last_batch AS (
    SELECT r.batch FROM run AS r WHERE r.type = 'send' ORDER BY r.started_at DESC, r.id DESC LIMIT 1)
SELECT 'setting'::character varying(7) AS type,
       s.id,
       s.path AS source,
       NULL::character varying(51) AS name,
       NULL::character varying(45) AS ip,
       NULL::integer AS port,
       NULL::character varying(51) AS address,
       NULL::character varying(32767) AS remote_path,
       NULL::timestamp with time zone AS recv_mtime,
       NULL::character varying(16) AS recv_text,
       NULL::timestamp with time zone AS sent_at,
       NULL::character varying(16) AS sent_text,
       NULL::integer AS ping_ms,
       NULL::timestamp with time zone AS ping_at,
       NULL::text AS fail_text,
       NULL::timestamp with time zone AS fail_at,
       NULL::boolean AS fail_cancelled,
       NULL::boolean AS in_last,
       NULL::character varying(7) AS event_type,
       NULL::character varying(14) AS at_text,
       NULL::integer AS file_count,
       NULL::character varying(16) AS kb_text,
       NULL::bigint AS took_ms,
       NULL::character varying(16) AS took_text,
       NULL::text AS note,
       NULL::boolean AS cancelled,
       0::bigint AS sort
  FROM dbo.vncscp AS s
 WHERE s.type = 'setting'
UNION ALL
SELECT 'machine', m.id, NULL, nullif(m.name, '')::character varying(51), m.ip, m.port, (m.ip || ':' || m.port)::character varying(51), coalesce(m.remote_path, ''),
       v.local_mtime,
       (CASE WHEN v.local_mtime IS NULL THEN '—'
             WHEN date_part('year', v.local_mtime) = date_part('year', now()) THEN to_char(v.local_mtime, 'MM-DD HH24:MI')
             ELSE to_char(v.local_mtime, 'YYYY-MM-DD') END)::character varying(16),
       v.finished_at,
       (CASE WHEN v.finished_at IS NULL THEN '—'
             WHEN date_part('year', v.finished_at) = date_part('year', now()) THEN to_char(v.finished_at, 'MM-DD HH24:MI')
             ELSE to_char(v.finished_at, 'YYYY-MM-DD') END)::character varying(16),
       p.connected_ms, p.started_at,
       l.note, CASE WHEN l.note IS NULL THEN NULL ELSE l.finished_at END, CASE WHEN l.note IS NULL THEN NULL ELSE l.cancelled_at IS NOT NULL END,
       m.id IN (SELECT r.machine_id FROM run AS r WHERE r.type = 'send' AND r.batch = (SELECT b.batch FROM last_batch AS b)),
       NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       row_number() OVER (ORDER BY coalesce(nullif(m.name, ''), m.ip), m.port)
  FROM machine AS m
  LEFT JOIN received AS v ON v.machine_id = m.id
  LEFT JOIN pinged AS p ON p.machine_id = m.id
  LEFT JOIN latest AS l ON l.machine_id = m.id
UNION ALL
SELECT 'event', r.id, NULL, r.machine_name::character varying(51), NULL, NULL, r.machine_address::character varying(51), r.remote_path,
       NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       r.type,
       to_char(r.started_at, 'MM-DD HH24:MI:SS')::character varying(14),
       r.file_count,
       to_char(coalesce(r.total_bytes, 0) / 1024.0, 'FM999,999,990.0')::character varying(16),
       r.took_ms,
       (CASE WHEN r.took_ms IS NULL THEN '—'
             WHEN r.took_ms < 1000 THEN r.took_ms || ' ms'
             ELSE to_char(r.took_ms / 1000.0, 'FM999990.0') || ' 秒' END)::character varying(16),
       r.note,
       r.cancelled_at IS NOT NULL,
       row_number() OVER (ORDER BY r.started_at DESC, r.id DESC)
  FROM run AS r
 WHERE r.finished_at IS NOT NULL
 ORDER BY type DESC, sort;

COMMENT ON VIEW dbo.vw_vncscp IS 'VNCSCP 主畫面：setting 一列是來源資料夾；machine 一台還在的機台一列，照名稱排；event 一次送出或測試一列，新的在前';
COMMENT ON COLUMN dbo.vw_vncscp.type IS 'setting＝設定；machine＝一台機台；event＝一次送出或測試';
COMMENT ON COLUMN dbo.vw_vncscp.id IS '[setting] 那一項設定的流水號；[machine] 機台的流水號；[event] send／test 那一列的流水號（寫回去照它換整列）';
COMMENT ON COLUMN dbo.vw_vncscp.source IS '[setting] 上次選的來源資料夾（setting_key 是 source 的那一列）；還沒選是空的';
COMMENT ON COLUMN dbo.vw_vncscp.name IS '[machine] 名稱，空的是空值；[event] 機台名稱，空的寫 IP:Port';
COMMENT ON COLUMN dbo.vw_vncscp.ip IS '[machine] IP';
COMMENT ON COLUMN dbo.vw_vncscp.port IS '[machine] UltraVNC 的 Port';
COMMENT ON COLUMN dbo.vw_vncscp.address IS '[machine][event] IP:Port';
COMMENT ON COLUMN dbo.vw_vncscp.remote_path IS '[machine] 現在的遠端路徑；[event] 這一次送到的路徑';
COMMENT ON COLUMN dbo.vw_vncscp.recv_mtime IS '[machine] 送到現在這個遠端路徑的最後一次成功，送的檔裡最新的修改時間；換過路徑就是空的';
COMMENT ON COLUMN dbo.vw_vncscp.recv_text IS '[machine] recv_mtime：今年寫 MM-DD HH24:MI、不是今年寫 YYYY-MM-DD；空的是破折號';
COMMENT ON COLUMN dbo.vw_vncscp.sent_at IS '[machine] 那一次成功送完的時刻';
COMMENT ON COLUMN dbo.vw_vncscp.sent_text IS '[machine] sent_at，寫法同 recv_text';
COMMENT ON COLUMN dbo.vw_vncscp.ping_ms IS '[machine] 最後一次登入成功（送或測）花了幾毫秒';
COMMENT ON COLUMN dbo.vw_vncscp.ping_at IS '[machine] 那一次開始的時刻';
COMMENT ON COLUMN dbo.vw_vncscp.fail_text IS '[machine] 最後一次送或測沒成功的原因；之後成功過就是空的';
COMMENT ON COLUMN dbo.vw_vncscp.fail_at IS '[machine] 那一次結束的時刻';
COMMENT ON COLUMN dbo.vw_vncscp.fail_cancelled IS '[machine] 那一次是使用者按了取消';
COMMENT ON COLUMN dbo.vw_vncscp.in_last IS '[machine] 最後一批送出裡有沒有這一台';
COMMENT ON COLUMN dbo.vw_vncscp.event_type IS '[event] send＝送出；test＝測試連線';
COMMENT ON COLUMN dbo.vw_vncscp.at_text IS '[event] 開始的時刻 MM-DD HH24:MI:SS';
COMMENT ON COLUMN dbo.vw_vncscp.file_count IS '[event] 送幾個檔（測試是空的）';
COMMENT ON COLUMN dbo.vw_vncscp.kb_text IS '[event] 要送的總大小 KB，一位小數、千分位';
COMMENT ON COLUMN dbo.vw_vncscp.took_ms IS '[event] 從開始到結束幾毫秒';
COMMENT ON COLUMN dbo.vw_vncscp.took_text IS '[event] took_ms：1 秒內寫 N ms，其餘 N.N 秒；空的是破折號';
COMMENT ON COLUMN dbo.vw_vncscp.note IS '[event] 沒成功的原因；成功是空的';
COMMENT ON COLUMN dbo.vw_vncscp.cancelled IS '[event] 這一次是使用者按了取消';
COMMENT ON COLUMN dbo.vw_vncscp.sort IS '[machine] 照名稱排；[event] 新的在前';

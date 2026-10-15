-- VNCSCP：設定、機台、每一次送出與測試
-- 一列是什麼由 type 決定；藍圖 .claude/blueprint/藍圖_VNCSCP.md

CREATE TABLE IF NOT EXISTS dbo.vncscp (
    id           serial PRIMARY KEY,
    type         text        NOT NULL,                 -- setting / machine / send / test
    setting_key  text,                                 -- [setting] source
    value        text,                                 -- [setting] 設定值
    name         text,                                 -- [machine] 機台名稱，可以空著
    ip           text,                                 -- [machine] IP
    port         integer,                              -- [machine] UltraVNC 的 Port
    remote_path  text,                                 -- [machine] 送到機台上的哪個資料夾；[send] 這一次送到哪裡
    machine_id   integer,                              -- [send][test] 哪一台
    batch        uuid,                                 -- [send][test] 同一次按下的那一批
    file_count   integer,                              -- [send] 送幾個檔
    total_bytes  bigint,                               -- [send] 要送的總位元組
    sent_bytes   bigint,                               -- [send] 實際送到的位元組
    local_mtime  timestamptz,                          -- [send] 送的檔裡最新的修改時間
    started_at   timestamptz,                          -- [send][test] 開始連線的時刻
    connected_ms integer,                              -- [send][test] 從開始到登入成功的毫秒
    finished_at  timestamptz,                          -- [send][test] 結束的時刻
    cancelled_at timestamptz,                          -- [send][test] 按取消的時刻
    note         text,                                 -- [send][test] 沒成功的原因
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now()
);

-- 設定的每一項
INSERT INTO dbo.vncscp (type, setting_key, value)
SELECT 'setting', 'source', ''
 WHERE NOT EXISTS (SELECT 1 FROM dbo.vncscp AS v WHERE v.type = 'setting' AND v.setting_key = 'source');

-- 表與每一欄的說明
COMMENT ON TABLE dbo.vncscp IS 'VNCSCP：設定、機台、每一次送出與測試，一列是什麼由 type 決定；VNC 密碼不在這裡，鎖在平台保險箱';
COMMENT ON COLUMN dbo.vncscp.id IS '流水號';
COMMENT ON COLUMN dbo.vncscp.type IS 'setting 一項設定／machine 一台機台／send 送一台的一次／test 測一台的一次';
COMMENT ON COLUMN dbo.vncscp.setting_key IS '[setting] 哪一項：source（上次選的來源資料夾）';
COMMENT ON COLUMN dbo.vncscp.value IS '[setting] 設定值';
COMMENT ON COLUMN dbo.vncscp.name IS '[machine] 機台名稱，可以空著';
COMMENT ON COLUMN dbo.vncscp.ip IS '[machine] IP';
COMMENT ON COLUMN dbo.vncscp.port IS '[machine] UltraVNC 的 Port';
COMMENT ON COLUMN dbo.vncscp.remote_path IS '[machine] 送到機台上的哪個資料夾；[send] 這一次送到哪裡';
COMMENT ON COLUMN dbo.vncscp.machine_id IS '[send][test] 哪一台（machine 那一列的 id）';
COMMENT ON COLUMN dbo.vncscp.batch IS '[send][test] 同一次按「送到勾選的」或「測試連線」的那一批';
COMMENT ON COLUMN dbo.vncscp.file_count IS '[send] 這一次送幾個檔';
COMMENT ON COLUMN dbo.vncscp.total_bytes IS '[send] 這一次要送的總位元組';
COMMENT ON COLUMN dbo.vncscp.sent_bytes IS '[send] 實際送到的位元組';
COMMENT ON COLUMN dbo.vncscp.local_mtime IS '[send] 這一次送的檔裡最新的修改時間，也就是機台收到的那一份';
COMMENT ON COLUMN dbo.vncscp.started_at IS '[send][test] 開始連線的時刻';
COMMENT ON COLUMN dbo.vncscp.connected_ms IS '[send][test] 從開始到登入成功花了幾毫秒；沒登入成功是 null';
COMMENT ON COLUMN dbo.vncscp.finished_at IS '[send][test] 結束的時刻：送完、測完、失敗、取消都是';
COMMENT ON COLUMN dbo.vncscp.cancelled_at IS '[send][test] 使用者按取消的時刻';
COMMENT ON COLUMN dbo.vncscp.note IS '[send][test] 沒成功的原因，照實寫；成功是 null';
COMMENT ON COLUMN dbo.vncscp.created_at IS '這一列建立的時刻';
COMMENT ON COLUMN dbo.vncscp.updated_at IS '這一列最後修改的時刻';

-- VNCSCP 主畫面：setting 一列（來源資料夾），machine 一台一列（機台那份、上次送達、上次回應、上次沒成功的原因、在不在最後一批送出裡），event 一次送或測一列（新的在前）
CREATE OR REPLACE VIEW dbo.vw_vncscp AS
WITH machine AS (
    SELECT m.* FROM dbo.vncscp AS m WHERE m.type = 'machine'),
run AS (
    SELECT r.*,
           coalesce(nullif(m.name, ''), m.ip || ':' || m.port) AS machine_name,
           (extract(epoch FROM (r.finished_at - r.started_at)) * 1000)::bigint AS took_ms
      FROM dbo.vncscp AS r
      JOIN machine AS m ON m.id = r.machine_id
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
SELECT 'setting'::text AS type,
       NULL::integer AS id,
       max(s.value) FILTER (WHERE s.setting_key = 'source') AS source,
       NULL::text AS name,
       NULL::text AS ip,
       NULL::integer AS port,
       NULL::text AS address,
       NULL::text AS remote_path,
       NULL::timestamptz AS recv_mtime,
       NULL::text AS recv_text,
       NULL::timestamptz AS sent_at,
       NULL::text AS sent_text,
       NULL::integer AS ping_ms,
       NULL::timestamptz AS ping_at,
       NULL::text AS fail_text,
       NULL::timestamptz AS fail_at,
       NULL::boolean AS fail_error,
       NULL::boolean AS in_last,
       NULL::text AS at_text,
       NULL::text AS action,
       NULL::text AS what,
       NULL::text AS took_text,
       NULL::text AS note,
       NULL::boolean AS danger,
       0::bigint AS sort
  FROM dbo.vncscp AS s
 WHERE s.type = 'setting'
UNION ALL
SELECT 'machine', m.id, NULL, nullif(m.name, ''), m.ip, m.port, m.ip || ':' || m.port, coalesce(m.remote_path, ''),
       v.local_mtime,
       CASE WHEN v.local_mtime IS NULL THEN '—'
            WHEN date_part('year', v.local_mtime) = date_part('year', now()) THEN to_char(v.local_mtime, 'MM-DD HH24:MI')
            ELSE to_char(v.local_mtime, 'YYYY-MM-DD') END,
       v.finished_at,
       CASE WHEN v.finished_at IS NULL THEN '—'
            WHEN date_part('year', v.finished_at) = date_part('year', now()) THEN to_char(v.finished_at, 'MM-DD HH24:MI')
            ELSE to_char(v.finished_at, 'YYYY-MM-DD') END,
       p.connected_ms, p.started_at,
       l.note, CASE WHEN l.note IS NULL THEN NULL ELSE l.finished_at END, CASE WHEN l.note IS NULL THEN NULL ELSE l.cancelled_at IS NULL END,
       m.id IN (SELECT r.machine_id FROM run AS r WHERE r.type = 'send' AND r.batch = (SELECT b.batch FROM last_batch AS b)),
       NULL, NULL, NULL, NULL, NULL, NULL,
       row_number() OVER (ORDER BY coalesce(nullif(m.name, ''), m.ip), m.port)
  FROM machine AS m
  LEFT JOIN received AS v ON v.machine_id = m.id
  LEFT JOIN pinged AS p ON p.machine_id = m.id
  LEFT JOIN latest AS l ON l.machine_id = m.id
UNION ALL
SELECT 'event', r.id, NULL, r.machine_name, NULL, NULL, NULL, r.remote_path,
       NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       to_char(r.started_at, 'MM-DD HH24:MI:SS'),
       CASE r.type WHEN 'send' THEN '送出' ELSE '測試連線' END,
       CASE r.type WHEN 'send' THEN r.file_count || ' 個檔 · ' || to_char(coalesce(r.total_bytes, 0) / 1024.0, 'FM999,999,990.0') || ' KB' || coalesce(' → ' || nullif(r.remote_path, ''), '')
            ELSE (SELECT m.ip || ':' || m.port FROM machine AS m WHERE m.id = r.machine_id) END,
       CASE WHEN r.took_ms IS NULL THEN '—'
            WHEN r.took_ms < 1000 THEN r.took_ms || ' ms'
            ELSE to_char(r.took_ms / 1000.0, 'FM999990.0') || ' 秒' END,
       r.note,
       r.note IS NOT NULL AND r.cancelled_at IS NULL,
       row_number() OVER (ORDER BY r.started_at DESC, r.id DESC)
  FROM run AS r
 WHERE r.finished_at IS NOT NULL
 ORDER BY type DESC, sort;

COMMENT ON VIEW dbo.vw_vncscp IS 'VNCSCP 主畫面：setting 一列是來源資料夾；machine 一台一列，照名稱排：位址、現在的遠端路徑、送到這個路徑的最後一次成功（機台那份的修改時間與文字、送達時刻與文字）、最後一次登入成功的毫秒與時刻、最後一次沒成功的原因與時刻（失敗或取消）、在不在最後一批送出裡；event 一次送或測一列，新的在前：時刻文字、動作、內容、耗時文字、原因、是不是失敗';

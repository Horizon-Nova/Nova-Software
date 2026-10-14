-- 操作說明：tool 加 guide（一步一個元素：text、image），放在 settings 後面，created_at、updated_at 仍是最後兩欄；vw_catalog 多 step 列與 image 欄

DROP VIEW IF EXISTS dbo.vw_shell, dbo.vw_catalog, dbo.vw_palette, dbo.vw_settings;

ALTER TABLE dbo.tool RENAME TO tool_old;
ALTER SEQUENCE dbo.tool_id_seq RENAME TO tool_old_id_seq;
ALTER TABLE dbo.tool_old RENAME CONSTRAINT tool_pkey TO tool_old_pkey;

CREATE TABLE dbo.tool (
    id           serial PRIMARY KEY,
    type         text NOT NULL,                       -- tool / key
    tool_no      text NOT NULL,                       -- 工具代碼
    name         text,                                -- [tool] 工具名稱
    icon         text,                                -- [tool] lucide 的圖示名稱
    cat          text,                                -- [tool] 分類
    vendor       text,                                -- [tool] 維護團隊
    version      text,                                -- [tool] 裝著的版本
    size         text,                                -- [tool] 套件大小
    updated      date,                                -- [tool] 匯入這一版的日期
    description  text,                                -- [tool] 說明
    capabilities jsonb NOT NULL DEFAULT '[]',         -- [tool] 這支工具會做的事
    permissions  jsonb NOT NULL DEFAULT '[]',         -- [tool] 會用到的權限
    changelog    jsonb NOT NULL DEFAULT '[]',         -- [tool] 版本與更新說明，一則一個元素，新到舊
    entry        text,                                -- [tool] 開起來要載入的組件檔名
    class        text,                                -- [tool] 組件裡那個畫面的型別全名
    home         text,                                -- [tool] 這支工具的檔案裝在哪一個資料夾
    source       text,                                -- [tool] 從哪裡來
    settings     text,                                -- [tool] 組件裡設定畫面的型別全名；沒有就是這支工具沒有設定
    guide        jsonb NOT NULL DEFAULT '[]',         -- [tool] 操作說明，一步一個元素：text、image（套件裡的相對路徑）
    code         text,                                -- [key] 操作員貼進來的那一串工具金鑰
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE dbo.tool IS '工具：一列是什麼由 type 決定。tool 是匯入的一支工具（匯入就是裝好，移除就是整列拿掉）；key 是啟用時存下的工具金鑰，最新的那一把就是現在用的；啟用、開啟、解鎖記在 event_log';
COMMENT ON COLUMN dbo.tool.id IS '流水號，也是側欄與工具目錄的先後';
COMMENT ON COLUMN dbo.tool.type IS 'tool 一支工具／key 一把工具金鑰';
COMMENT ON COLUMN dbo.tool.tool_no IS '工具代碼，套件 manifest 的 tool_no；key 用它對到那一支工具';
COMMENT ON COLUMN dbo.tool.name IS '[tool] 工具名稱';
COMMENT ON COLUMN dbo.tool.icon IS '[tool] lucide 的圖示名稱';
COMMENT ON COLUMN dbo.tool.cat IS '[tool] 分類';
COMMENT ON COLUMN dbo.tool.vendor IS '[tool] 維護團隊';
COMMENT ON COLUMN dbo.tool.version IS '[tool] 裝著的版本';
COMMENT ON COLUMN dbo.tool.size IS '[tool] 套件大小，例如 42.8 KB';
COMMENT ON COLUMN dbo.tool.updated IS '[tool] 匯入這一版的日期';
COMMENT ON COLUMN dbo.tool.description IS '[tool] 說明';
COMMENT ON COLUMN dbo.tool.capabilities IS '[tool] 這支工具會做的事，字串陣列';
COMMENT ON COLUMN dbo.tool.permissions IS '[tool] 會用到的權限，字串陣列';
COMMENT ON COLUMN dbo.tool.changelog IS '[tool] 版本與更新說明，一則一個元素（ver、date、note），新到舊';
COMMENT ON COLUMN dbo.tool.entry IS '[tool] 開起來要載入的組件檔名，例如 InSpecTool.dll';
COMMENT ON COLUMN dbo.tool.class IS '[tool] 組件裡那個畫面的型別全名，例如 InSpecTool.Measure';
COMMENT ON COLUMN dbo.tool.home IS '[tool] 這支工具的檔案裝在哪一個資料夾';
COMMENT ON COLUMN dbo.tool.source IS '[tool] 從哪裡來，例如 本機套件檔';
COMMENT ON COLUMN dbo.tool.settings IS '[tool] 組件裡設定畫面的型別全名，例如 InSpecTool.Setup；沒有就是這支工具沒有設定';
COMMENT ON COLUMN dbo.tool.guide IS '[tool] 操作說明，一步一個元素（text 這一步做什麼、image 截圖在套件裡的相對路徑，沒有圖就不寫），照步驟順序';
COMMENT ON COLUMN dbo.tool.code IS '[key] 操作員貼進來的那一串工具金鑰；每次開工具都重驗一次';
COMMENT ON COLUMN dbo.tool.created_at IS '寫入時刻：tool 是第一次匯入、key 是貼進來的時刻';
COMMENT ON COLUMN dbo.tool.updated_at IS '最後修改時刻：tool 是最後一次匯入';

INSERT INTO dbo.tool (id, type, tool_no, name, icon, cat, vendor, version, size, updated, description, capabilities, permissions, changelog, entry, class, home, source, settings, code, created_at, updated_at)
SELECT id, type, tool_no, name, icon, cat, vendor, version, size, updated, description, capabilities, permissions, changelog, entry, class, home, source, settings, code, created_at, updated_at FROM dbo.tool_old ORDER BY id;
SELECT setval('dbo.tool_id_seq', (SELECT max(id) FROM dbo.tool));
DROP TABLE dbo.tool_old;

CREATE OR REPLACE VIEW dbo.vw_shell AS
SELECT 'shell'::text AS type,
       NULL::text AS tool_no,
       NULL::text AS name,
       NULL::text AS icon,
       NULL::text AS entry,
       NULL::text AS class,
       NULL::text AS home,
       NULL::text AS code,
       (SELECT count(*) FROM dbo.tool AS t WHERE t.type = 'tool')::integer AS tool_count,
       coalesce((SELECT to_char(max(t.updated_at), 'YYYY-MM-DD HH24:MI') FROM dbo.tool AS t WHERE t.type = 'tool'), '—') AS sync_text,
       0 AS sort
UNION ALL
SELECT 'tool', t.tool_no, t.name, t.icon, t.entry, t.class, t.home,
       (SELECT k.code FROM dbo.tool AS k WHERE k.type = 'key' AND k.tool_no = t.tool_no ORDER BY k.created_at DESC, k.id DESC LIMIT 1),
       NULL, NULL, t.id
  FROM dbo.tool AS t
 WHERE t.type = 'tool'
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_shell IS '外殼：shell 一列是工具總數與最後匯入的時刻；tool 一支工具一列，側欄照 sort 排，開工具時讀它的組件、畫面型別、資料夾與最新那一把工具金鑰';

CREATE OR REPLACE VIEW dbo.vw_catalog AS
SELECT 'total'::text AS type,
       NULL::text AS cat,
       count(*)::integer AS tool_count,
       NULL::text AS tool_no,
       NULL::text AS name,
       NULL::text AS icon,
       NULL::text AS vendor,
       NULL::text AS description,
       NULL::text AS version_text,
       NULL::text AS size,
       NULL::text AS updated_text,
       NULL::text[] AS capabilities,
       NULL::text[] AS permissions,
       NULL::text AS home,
       NULL::text AS last_open_text,
       NULL::integer AS opens,
       NULL::text AS expires_text,
       NULL::text AS ver,
       NULL::text AS date,
       NULL::text AS note,
       NULL::boolean AS latest,
       NULL::text AS term,
       NULL::text AS image,
       0::bigint AS sort
  FROM dbo.tool AS t
 WHERE t.type = 'tool'
UNION ALL
SELECT 'cat', t.cat, count(*)::integer, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, min(t.id)
  FROM dbo.tool AS t
 WHERE t.type = 'tool'
 GROUP BY t.cat
UNION ALL
SELECT 'tool', t.cat, NULL, t.tool_no, t.name, t.icon, t.vendor, t.description, 'v' || t.version, t.size,
       to_char(t.updated, 'YYYY/MM/DD'),
       ARRAY(SELECT jsonb_array_elements_text(t.capabilities)),
       ARRAY(SELECT jsonb_array_elements_text(t.permissions)),
       t.home,
       to_char(u.last_open, 'YYYY-MM-DD HH24:MI'),
       coalesce(u.opens, 0)::integer,
       CASE WHEN k.code IS NULL THEN NULL
            ELSE coalesce(nullif(convert_from(decode(k.code, 'base64'), 'UTF8')::jsonb ->> 'Expires', ''), '永久') END,
       NULL, NULL, NULL, NULL, NULL, NULL,
       row_number() OVER (ORDER BY u.last_open DESC NULLS LAST, t.id)
  FROM dbo.tool AS t
  LEFT JOIN LATERAL (
        SELECT one.code
          FROM dbo.tool AS one
         WHERE one.type = 'key' AND one.tool_no = t.tool_no
         ORDER BY one.created_at DESC, one.id DESC
         LIMIT 1) AS k ON true
  LEFT JOIN LATERAL (
        SELECT count(*) FILTER (WHERE e.type = 'open') AS opens,
               max(e.created_at) FILTER (WHERE e.type = 'open') AS last_open
          FROM dbo.event_log AS e
         WHERE e.tool_no = t.tool_no) AS u ON true
 WHERE t.type = 'tool'
UNION ALL
SELECT 'change', NULL, NULL, t.tool_no, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       c.item ->> 'ver', c.item ->> 'date', c.item ->> 'note', c.at = 1, NULL, NULL, c.at
  FROM dbo.tool AS t
 CROSS JOIN LATERAL jsonb_array_elements(t.changelog) WITH ORDINALITY AS c(item, at)
 WHERE t.type = 'tool'
UNION ALL
SELECT 'step', NULL, NULL, t.tool_no, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       g.item ->> 'text', NULL, NULL,
       CASE WHEN coalesce(g.item ->> 'image', '') = '' THEN NULL ELSE t.home || chr(92) || replace(g.item ->> 'image', '/', chr(92)) END,
       g.at
  FROM dbo.tool AS t
 CROSS JOIN LATERAL jsonb_array_elements(t.guide) WITH ORDINALITY AS g(item, at)
 WHERE t.type = 'tool'
UNION ALL
SELECT 'search', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, s.term, NULL,
       row_number() OVER (ORDER BY s.used_at DESC)
  FROM dbo.search AS s
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_catalog IS '工具目錄：total 是全部幾支；cat 一個分類一列與它有幾支；tool 一支工具一列，清單的維護團隊、版本、最後開啟、開啟次數、金鑰到期（沒有到期日是永久），右側的說明、匯入日期、大小、能力與權限陣列、裝在哪個資料夾，最近開過的在前；change 一則更新日誌一列，新到舊，latest 是最新那一則；step 操作說明一步一列，note 是這一步做什麼、image 是截圖的完整路徑（沒有圖是 NULL）；search 一個搜尋過的字一列，最近用到的在前';

CREATE OR REPLACE VIEW dbo.vw_palette AS
SELECT 'tool'::text AS type,
       t.tool_no,
       t.name,
       t.icon,
       t.vendor,
       NULL::text AS term,
       (SELECT count(*) FROM dbo.tool AS a WHERE a.type = 'tool')::integer AS tool_count,
       t.id::bigint AS sort
  FROM dbo.tool AS t
 WHERE t.type = 'tool'
UNION ALL
SELECT 'search', NULL, NULL, NULL, NULL, s.term,
       (SELECT count(*) FROM dbo.tool AS a WHERE a.type = 'tool')::integer,
       row_number() OVER (ORDER BY s.used_at DESC)
  FROM dbo.search AS s
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_palette IS '指令面板：tool 一支工具一列；search 一個搜尋過的字一列，最近用到的在前；tool_count 是工具總數';

CREATE OR REPLACE VIEW dbo.vw_settings AS
SELECT 'setting'::text AS type,
       s.id,
       s.area_key,
       s.area_label,
       s.section_key,
       s.section_label,
       s.section_note,
       s.group_label,
       s.setting_key,
       s.kind,
       s.label,
       s.inline,
       s.note,
       s.value,
       s.fallback,
       s.options,
       s.danger,
       s.button,
       NULL::text AS tool_no,
       NULL::text AS name,
       NULL::text AS entry,
       NULL::text AS home,
       NULL::text AS settings,
       NULL::integer AS unlock_minutes,
       NULL::text AS expires_text,
       NULL::text AS activated_text,
       NULL::integer AS opens,
       NULL::text AS last_open_text,
       NULL::integer AS unlocks,
       NULL::text AS last_unlock_text,
       s.sort::bigint AS sort
  FROM dbo.setting AS s
UNION ALL
SELECT 'tool', t.id, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       t.tool_no, t.name, t.entry, t.home, t.settings,
       (SELECT nullif(regexp_replace(m.value, '\D', '', 'g'), '')::integer FROM dbo.setting AS m WHERE m.setting_key = 'toolKeyMinutes'),
       CASE WHEN k.code IS NULL THEN NULL
            ELSE coalesce(nullif(convert_from(decode(k.code, 'base64'), 'UTF8')::jsonb ->> 'Expires', ''), '永久') END,
       to_char(u.activated_at, 'YYYY-MM-DD HH24:MI'),
       coalesce(u.opens, 0)::integer,
       to_char(u.last_open, 'YYYY-MM-DD HH24:MI'),
       coalesce(u.unlocks, 0)::integer,
       to_char(u.last_unlock, 'YYYY-MM-DD HH24:MI'),
       row_number() OVER (ORDER BY greatest(u.last_open, u.last_unlock, u.activated_at) DESC NULLS LAST, t.name)
  FROM dbo.tool AS t
  LEFT JOIN LATERAL (
        SELECT one.code
          FROM dbo.tool AS one
         WHERE one.type = 'key' AND one.tool_no = t.tool_no
         ORDER BY one.created_at DESC, one.id DESC
         LIMIT 1) AS k ON true
  LEFT JOIN LATERAL (
        SELECT min(e.created_at) FILTER (WHERE e.type = 'activate') AS activated_at,
               count(*) FILTER (WHERE e.type = 'open') AS opens,
               max(e.created_at) FILTER (WHERE e.type = 'open') AS last_open,
               count(*) FILTER (WHERE e.type = 'unlock') AS unlocks,
               max(e.created_at) FILTER (WHERE e.type = 'unlock') AS last_unlock
          FROM dbo.event_log AS e
         WHERE e.tool_no = t.tool_no) AS u ON true
 WHERE t.type = 'tool'
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_settings IS '設定：setting 一項設定一列，照 sort；tool 一支工具一列，有 settings 的才有設定畫面，帶解鎖分鐘數（從 toolKeyMinutes 取數字）、工具金鑰到期（沒有到期日是永久）、啟用時刻、開啟與解鎖次數和最後一次，最近用過的在前';

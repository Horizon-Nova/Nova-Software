-- 一個畫面一支 view；同一個畫面要的不同東西用 type 區分，過濾、計數、排序、格式化都在這裡做完

-- 外殼（側欄、狀態列、開工具）：shell 一列（工具總數、最後匯入），tool 一支工具一列（開它要的檔與存著的工具金鑰）
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

-- 工具目錄：total 一列（全部幾支），cat 一個分類一列（幾支），tool 一支工具一列（清單與右側資訊要的全部欄位；最近開過的在前），change 一則更新日誌一列（新到舊），search 一個搜尋過的字一列（同一個字搜過幾次都只一列，最近一次搜的在前）
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
       row_number() OVER (ORDER BY max(s.created_at) DESC)
  FROM dbo.search AS s
 GROUP BY s.term
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_catalog IS '工具目錄：total 是全部幾支；cat 一個分類一列與它有幾支；tool 一支工具一列，清單的維護團隊、版本、最後開啟、開啟次數、金鑰到期（沒有到期日是永久），右側的說明、匯入日期、大小、能力與權限陣列、裝在哪個資料夾，最近開過的在前；change 一則更新日誌一列，新到舊，latest 是最新那一則；step 操作說明一步一列，note 是這一步做什麼、image 是截圖的完整路徑（沒有圖是 NULL）；search 一個搜尋過的字一列，最近用到的在前';

-- 指令面板：tool 一支工具一列，search 一個搜尋過的字一列（同一個字搜過幾次都只一列，最近一次搜的在前）；每一列都帶工具總數
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
       row_number() OVER (ORDER BY max(s.created_at) DESC)
  FROM dbo.search AS s
 GROUP BY s.term
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_palette IS '指令面板：tool 一支工具一列；search 一個搜尋過的字一列，最近用到的在前；tool_count 是工具總數';

-- 設定：setting 一項設定一列（照 sort），tool 一支工具一列（設定畫面、解鎖分鐘數、工具金鑰到期、啟用與使用次數；最近用過的在前）
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

-- InSpecTool 量測頁：setting 一列（每一項設定一欄，型別轉好），field 一個欄位一列（照順序），run 一次執行一列（新的在前；程式檔名、照欄位順序的欄位值、花了幾秒、有沒有歸檔）
CREATE OR REPLACE VIEW dbo.vw_inspec_measure AS
SELECT 'setting'::text AS type,
       NULL::integer AS id,
       max(i.value) FILTER (WHERE i.setting_key = 'searchFolder') AS search_folder,
       max(i.value) FILTER (WHERE i.setting_key = 'logFolder') AS log_folder,
       max(i.value) FILTER (WHERE i.setting_key = 'backupFolder') AS backup_folder,
       max(i.value) FILTER (WHERE i.setting_key = 'scanInterval')::integer AS scan_interval,
       coalesce(max(i.value) FILTER (WHERE i.setting_key = 'showResult') = 'true', false) AS show_result,
       nullif(max(i.value) FILTER (WHERE i.setting_key = 'leftWidth'), '')::double precision AS left_width,
       NULL::text AS name,
       NULL::text AS prompt,
       NULL::text AS mode,
       NULL::boolean AS confirm,
       NULL::boolean AS editable,
       NULL::boolean AS archived,
       NULL::text AS started_text,
       NULL::text AS elapsed_text,
       NULL::text AS archive,
       NULL::text AS note,
       NULL::text AS program_name,
       NULL::text[] AS field_values,
       NULL::text AS seconds_text,
       NULL::text AS run_text,
       0::bigint AS sort
  FROM dbo.inspec AS i
 WHERE i.type = 'setting'
UNION ALL
SELECT 'field', i.id, NULL, NULL, NULL, NULL, NULL, NULL,
       i.name, i.prompt, i.mode, i.mode = 'confirm', coalesce(i.editable, false), NULL, NULL, NULL, NULL, NULL,
       NULL, NULL, NULL, NULL, i.sort
  FROM dbo.inspec AS i
 WHERE i.type = 'field'
UNION ALL
SELECT 'run', i.id, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       i.archive IS NOT NULL,
       to_char(i.started_at, 'YYYY-MM-DD HH24:MI:SS'),
       CASE WHEN i.finished_at IS NULL THEN NULL
            ELSE to_char(EXTRACT(EPOCH FROM (i.finished_at - i.started_at)), 'FM999990.0') || ' 秒' END,
       i.archive,
       i.note,
       reverse(split_part(reverse(coalesce(i.program, '')), chr(92), 1)),
       ARRAY(SELECT coalesce(i.fields ->> f.name, '')
               FROM dbo.inspec AS f
              WHERE f.type = 'field'
              ORDER BY f.sort, f.id),
       CASE WHEN i.finished_at IS NULL THEN NULL
            ELSE to_char(EXTRACT(EPOCH FROM (i.finished_at - i.started_at)), 'FM999990.0') END,
       to_char(i.started_at, 'MM-DD HH24:MI'),
       row_number() OVER (ORDER BY i.started_at DESC, i.id DESC)
  FROM dbo.inspec AS i
 WHERE i.type = 'run'
 ORDER BY type DESC, sort;

COMMENT ON VIEW dbo.vw_inspec_measure IS 'InSpecTool 量測頁：setting 一列是全部設定（搜尋資料夾、紀錄資料夾、備份路徑、解析間隔毫秒、跑完顯示結果、左欄寬 px）；field 一個欄位一列，照順序；run 一次執行一列，新的在前：有沒有歸檔、開始時刻文字、花了多久文字、歸檔路徑、備註、程式檔名、照欄位順序排的欄位值、秒數、月日時分';

-- InSpecTool 設定：setting 一列（每一項設定一欄，解析間隔的選項攤成陣列），field 一個欄位一列（照順序）
CREATE OR REPLACE VIEW dbo.vw_inspec_setup AS
SELECT 'setting'::text AS type,
       max(i.value) FILTER (WHERE i.setting_key = 'searchFolder') AS search_folder,
       max(i.value) FILTER (WHERE i.setting_key = 'logFolder') AS log_folder,
       max(i.value) FILTER (WHERE i.setting_key = 'backupFolder') AS backup_folder,
       max(i.value) FILTER (WHERE i.setting_key = 'scanInterval')::integer AS scan_interval,
       ARRAY(SELECT jsonb_array_elements_text(max(i.options::text) FILTER (WHERE i.setting_key = 'scanInterval')::jsonb)) AS scan_options,
       coalesce(max(i.value) FILTER (WHERE i.setting_key = 'showResult') = 'true', false) AS show_result,
       NULL::text AS name,
       NULL::text AS prompt,
       NULL::boolean AS confirm,
       NULL::boolean AS editable,
       0 AS sort
  FROM dbo.inspec AS i
 WHERE i.type = 'setting'
UNION ALL
SELECT 'field', NULL, NULL, NULL, NULL, NULL, NULL,
       i.name, i.prompt, i.mode = 'confirm', coalesce(i.editable, false), i.sort
  FROM dbo.inspec AS i
 WHERE i.type = 'field'
 ORDER BY type DESC, sort;

COMMENT ON VIEW dbo.vw_inspec_setup IS 'InSpecTool 設定：setting 一列是全部設定與解析間隔的選項；field 一個欄位一列，照順序';

-- 日誌：count 一種事件一列（幾筆，多的在前），event 一件一列（清單與右邊明細要的全部欄位，新的在前）；事件名照 event_log.type 的註解，工具移除了就顯示代碼
CREATE OR REPLACE VIEW dbo.vw_event_log AS
WITH named AS (
    SELECT e.*,
           CASE e.type WHEN 'activate' THEN '啟用工具' WHEN 'open' THEN '開啟工具' WHEN 'unlock' THEN '解鎖設定' WHEN 'error' THEN '錯誤' ELSE e.type END AS event_name
      FROM dbo.event_log AS e)
SELECT 'count'::text AS type,
       NULL::integer AS id,
       n.type AS event_type,
       n.event_name,
       count(*)::integer AS event_count,
       NULL::text AS tool_no,
       NULL::text AS tool_name,
       NULL::text AS account,
       NULL::text AS function_name,
       NULL::text AS message,
       NULL::text AS stack_trace,
       NULL::timestamptz AS created_at,
       NULL::text AS created_text,
       row_number() OVER (ORDER BY count(*) DESC, n.type)::integer AS sort
  FROM named AS n
 GROUP BY n.type, n.event_name
UNION ALL
SELECT 'event', n.id, n.type, n.event_name, NULL,
       n.tool_no, coalesce(t.name, n.tool_no), n.account, n.function_name, n.message, n.stack_trace,
       n.created_at, to_char(n.created_at, 'YYYY-MM-DD HH24:MI:SS'),
       row_number() OVER (ORDER BY n.created_at DESC, n.id DESC)::integer
  FROM named AS n
  LEFT JOIN dbo.tool AS t ON t.type = 'tool' AND t.tool_no = n.tool_no
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_event_log IS '日誌：count 一種事件一列（幾筆，多的在前）；event 一件一列（時間、事件名、工具名稱、帳號、方法、訊息、呼叫堆疊，新的在前）';
-- 工具目錄併進工具資訊（vw_detail 拿掉）；量測頁加最近執行要的欄；設定拿掉「常用」裡重複的兩項

DROP VIEW IF EXISTS dbo.vw_catalog, dbo.vw_detail, dbo.vw_inspec_measure;

-- 工具目錄：total 一列（全部幾支），cat 一個分類一列（幾支），tool 一支工具一列（清單與右側資訊要的全部欄位；最近開過的在前），change 一則更新日誌一列（新到舊），search 一個搜尋過的字一列（最近的在前）
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
       0::bigint AS sort
  FROM dbo.tool AS t
 WHERE t.type = 'tool'
UNION ALL
SELECT 'cat', t.cat, count(*)::integer, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, min(t.id)
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
       NULL, NULL, NULL, NULL, NULL,
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
       c.item ->> 'ver', c.item ->> 'date', c.item ->> 'note', c.at = 1, NULL, c.at
  FROM dbo.tool AS t
 CROSS JOIN LATERAL jsonb_array_elements(t.changelog) WITH ORDINALITY AS c(item, at)
 WHERE t.type = 'tool'
UNION ALL
SELECT 'search', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, s.term,
       row_number() OVER (ORDER BY s.used_at DESC)
  FROM dbo.search AS s
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_catalog IS '工具目錄：total 是全部幾支；cat 一個分類一列與它有幾支；tool 一支工具一列，清單的維護團隊、版本、最後開啟、開啟次數、金鑰到期（沒有到期日是永久），右側的說明、匯入日期、大小、能力與權限陣列、裝在哪個資料夾，最近開過的在前；change 一則更新日誌一列，新到舊，latest 是最新那一則；search 一個搜尋過的字一列，最近用到的在前';

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

DELETE FROM dbo.setting WHERE area_key = 'top';

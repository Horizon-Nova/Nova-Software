-- 搜尋過的字改成一次搜尋一列：拿掉 used_at 與 term 的唯一，同一個字幾次、最近一次由 view 分組算

DROP VIEW IF EXISTS dbo.vw_catalog, dbo.vw_palette;

UPDATE dbo.search SET created_at = used_at;

ALTER TABLE dbo.search DROP CONSTRAINT IF EXISTS search_term_key;
ALTER TABLE dbo.search DROP COLUMN IF EXISTS used_at;

COMMENT ON TABLE dbo.search IS '搜尋過的字：一次搜尋一列';
COMMENT ON COLUMN dbo.search.created_at IS '這一次搜尋的時刻';

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

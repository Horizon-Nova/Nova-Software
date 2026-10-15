-- 日誌頁：新增 vw_event_log

DROP VIEW IF EXISTS dbo.vw_event_log;
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
-- 使用紀錄表：vw_key 帶出金鑰到期的文字

-- 每支工具一列：存著的金鑰、它的到期日、啟用時刻、開啟與解鎖的次數和最後一次；最近用過的排前面
CREATE OR REPLACE VIEW dbo.vw_key AS
SELECT t.tool_no,
       t.name,
       k.code,
       CASE WHEN k.code IS NULL THEN NULL
            ELSE convert_from(decode(k.code, 'base64'), 'UTF8')::jsonb ->> 'Expires' END AS expires,
       to_char(u.activated_at, 'YYYY-MM-DD HH24:MI') AS activated_text,
       coalesce(u.opens, 0) AS opens,
       to_char(u.last_open, 'YYYY-MM-DD HH24:MI') AS last_open_text,
       coalesce(u.unlocks, 0) AS unlocks,
       to_char(u.last_unlock, 'YYYY-MM-DD HH24:MI') AS last_unlock_text,
       greatest(u.last_open, u.last_unlock, u.activated_at) AS last_used,
       CASE WHEN k.code IS NULL THEN NULL
            ELSE coalesce(nullif(convert_from(decode(k.code, 'base64'), 'UTF8')::jsonb ->> 'Expires', ''), '永久') END AS expires_text
  FROM dbo.tool_management AS t
  LEFT JOIN LATERAL (
        SELECT one.code
          FROM dbo.key_management AS one
         WHERE one.type = 'key' AND one.tool_no = t.tool_no
         ORDER BY one.created_at DESC, one.id DESC
         LIMIT 1) AS k ON true
  LEFT JOIN LATERAL (
        SELECT min(one.created_at) FILTER (WHERE one.action = 'activate') AS activated_at,
               count(*) FILTER (WHERE one.action = 'open') AS opens,
               max(one.created_at) FILTER (WHERE one.action = 'open') AS last_open,
               count(*) FILTER (WHERE one.action = 'unlock') AS unlocks,
               max(one.created_at) FILTER (WHERE one.action = 'unlock') AS last_unlock
          FROM dbo.key_management AS one
         WHERE one.type = 'use' AND one.tool_no = t.tool_no) AS u ON true
 WHERE t.type = 'tool'
 ORDER BY greatest(u.last_open, u.last_unlock, u.activated_at) DESC NULLS LAST, t.name;

COMMENT ON VIEW dbo.vw_key IS '每支工具一列：工具金鑰、到期日（expires_text：沒有到期日是永久，沒有金鑰是 NULL）、啟用時刻、開啟與解鎖設定的次數和最後一次；最近用過的排前面';

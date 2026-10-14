-- 工具金鑰：存著的那一把與每一次使用、每支工具一列的 view、工具設定解鎖多久；InSpecTool 自己的解鎖號碼拿掉，改由平台的工具金鑰解鎖


CREATE TABLE IF NOT EXISTS dbo.key_management (
    id          serial PRIMARY KEY,
    type        text NOT NULL,                     -- key（存著的一把工具金鑰）／use（用了一次）
    tool_no     text NOT NULL,                     -- 哪一支工具
    code        text,                              -- [key] 貼進來的那一串
    action      text,                              -- [use] activate（啟用）／open（開啟）／unlock（解鎖設定）
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE dbo.key_management IS '工具金鑰：type=key 是啟用時存下的那一把，type=use 是每一次啟用、開啟、解鎖設定';
COMMENT ON COLUMN dbo.key_management.id IS '流水號';
COMMENT ON COLUMN dbo.key_management.type IS 'key（存著的一把工具金鑰）／use（用了一次）';
COMMENT ON COLUMN dbo.key_management.tool_no IS '哪一支工具，對到 tool_management.tool_no';
COMMENT ON COLUMN dbo.key_management.code IS '[key] 操作員貼進來的那一串工具金鑰；每次開工具都重驗一次';
COMMENT ON COLUMN dbo.key_management.action IS '[use] activate（啟用）／open（開啟）／unlock（解鎖設定）';
COMMENT ON COLUMN dbo.key_management.created_at IS '寫入時刻；use 這一列就是用的那一刻';
COMMENT ON COLUMN dbo.key_management.updated_at IS '最後修改時刻';

-- 每支裝好的工具一列：存著的金鑰、它的到期日、啟用時刻、開啟與解鎖的次數和最後一次；最近用過的排前面
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
       greatest(u.last_open, u.last_unlock, u.activated_at) AS last_used
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
 WHERE t.type = 'tool' AND t.installed_version IS NOT NULL
 ORDER BY greatest(u.last_open, u.last_unlock, u.activated_at) DESC NULLS LAST, t.name;

COMMENT ON VIEW dbo.vw_key IS '每支裝好的工具一列：工具金鑰、到期日、啟用時刻、開啟與解鎖設定的次數和最後一次；最近用過的排前面';

-- 工具設定解鎖多久：關於本軟體 › 工具金鑰
INSERT INTO dbo.setting_management (area_key, area_label, section_key, section_label, section_note, group_label, setting_key, kind, label, inline, note, value, fallback, options, danger, sort)
SELECT 'support', '支援', 'about', '關於本軟體', NULL, '工具金鑰', 'toolKeyMinutes', 'choice', '工具設定解鎖多久', NULL,
       '輸入工具金鑰後，工具設定開放修改的時間；時間到自動鎖回，金鑰不留在這台電腦。', '5 分鐘', '5 分鐘', '["5 分鐘"]'::jsonb, false, 32
 WHERE NOT EXISTS (SELECT 1 FROM dbo.setting_management WHERE setting_key = 'toolKeyMinutes');

-- InSpecTool 欄位結構的解鎖號碼不再用
DELETE FROM dbo.inspec_management WHERE type = 'setting' AND setting_key = 'unlockCode';
COMMENT ON COLUMN dbo.inspec_management.setting_key IS '[setting] 哪一項：searchFolder、logFolder、backupFolder、scanInterval、showResult、leftWidth（量測頁左欄拖到多寬，px；空的就照版面比例）';

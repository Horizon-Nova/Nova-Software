-- InSpecTool 量測頁中間的分隔可以拖：記住左欄拖到多寬

INSERT INTO dbo.inspec_management (type, setting_key, value, options)
SELECT 'setting', 'leftWidth', '', '[]'::jsonb
 WHERE NOT EXISTS (SELECT 1 FROM dbo.inspec_management WHERE type = 'setting' AND setting_key = 'leftWidth');

COMMENT ON COLUMN dbo.inspec_management.setting_key IS '[setting] 哪一項：searchFolder、logFolder、backupFolder、scanInterval、showResult、unlockCode、leftWidth（量測頁左欄拖到多寬，px；空的就照版面比例）';

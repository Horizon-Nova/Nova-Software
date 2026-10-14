-- 工具金鑰：金鑰與每一次使用是 tool 表的 key、use 兩種列（見 01_schema.sql）；這裡只放解鎖時間那一項設定
-- 藍圖 .claude/blueprint/藍圖_工具金鑰.md

-- 工具設定解鎖多久：關於本軟體 › 工具金鑰
INSERT INTO dbo.setting (area_key, area_label, section_key, section_label, section_note, group_label, setting_key, kind, label, inline, note, value, fallback, options, danger, sort)
SELECT 'support', '支援', 'about', '關於本軟體', NULL, '工具金鑰', 'toolKeyMinutes', 'choice', '工具設定解鎖多久', NULL,
       '輸入工具金鑰後，工具設定開放修改的時間；時間到自動鎖回，金鑰不留在這台電腦。', '5 分鐘', '5 分鐘', '["5 分鐘"]'::jsonb, false, 32
 WHERE NOT EXISTS (SELECT 1 FROM dbo.setting WHERE setting_key = 'toolKeyMinutes');

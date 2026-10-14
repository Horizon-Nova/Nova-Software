-- 拿掉設計稿留下的佔位工具與假資訊

-- 佔位工具：只剩真的匯入過的工具
DELETE FROM dbo.tool_management
 WHERE tool_no IN ('sec.agent', 'it.remote', 'inbox', 'qr.gen', 'vault', 'asset.query', 'file.send', 'notice.kb', 'print.queue', 'translate', 'timeclock');

-- 假裝置、假使用者、預先記著的搜尋字
DELETE FROM dbo.device_management;
DELETE FROM dbo.search_management;

-- 寫死的關於資訊、沒有作用的診斷包、公告與工單提醒
DELETE FROM dbo.setting_management WHERE sort IN (13, 14, 25, 26, 27, 28, 29, 30, 31);
UPDATE dbo.setting_management SET note = NULL WHERE sort = 12;
UPDATE dbo.setting_management SET section_note = NULL WHERE section_key IN ('notify', 'about');

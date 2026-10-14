-- 設定：拿掉沒有作用的幾項（自動更新、更新時段、主管核可、保持登入、檢查更新），「更新與安裝」那一節只剩套件庫資料夾

DELETE FROM dbo.setting_management
 WHERE setting_key IN ('updateWindow', 'updateAuto', 'updateNeedApproval', 'keepSignedIn')
    OR (kind = 'action' AND label = '立即檢查更新');

UPDATE dbo.setting_management
   SET section_label = '工具套件', section_note = NULL, group_label = '套件庫'
 WHERE section_key = 'update';

-- 開啟後顯示：選項「上次用的工具」沒有實作、「IT 必裝分類」已不存在，實際只會停在工具目錄
DELETE FROM dbo.setting_management WHERE setting_key = 'startView';

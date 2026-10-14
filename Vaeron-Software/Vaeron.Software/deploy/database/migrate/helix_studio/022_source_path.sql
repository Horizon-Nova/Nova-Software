-- 套件庫資料夾不再寫死在種子資料：清掉當初種進去的那一個開發機路徑，平台啟動時照這台電腦這個帳號自己填；使用者自己選過的路徑不動

UPDATE dbo.setting
   SET value = NULL, fallback = NULL, options = '[]'::jsonb, updated_at = now()
 WHERE setting_key = 'source'
   AND value = 'C:\Users\user\AppData\Local\HelixStudio\library';

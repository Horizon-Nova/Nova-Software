-- 大按鈕模式改成預設關：建議值（回復預設寫回的值）一律改成 false；
-- 現在的值只在從種下來就沒被動過時（updated_at 仍等於 created_at）跟著改，使用者自己開過的不動

UPDATE dbo.setting
   SET value = CASE WHEN updated_at = created_at THEN 'false' ELSE value END,
       fallback = 'false'
 WHERE setting_key = 'bigTargets';

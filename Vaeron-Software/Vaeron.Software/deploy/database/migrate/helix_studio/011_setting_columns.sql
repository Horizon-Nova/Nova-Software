-- 設定表：最後兩欄補上 created_at、updated_at，每一欄寫 COMMENT

ALTER TABLE dbo.setting_management
    ADD COLUMN IF NOT EXISTS created_at timestamptz NOT NULL DEFAULT now(),
    ADD COLUMN IF NOT EXISTS updated_at timestamptz NOT NULL DEFAULT now();

COMMENT ON TABLE dbo.setting_management IS '平台設定：一列一項，種類寫在 kind，選項放 jsonb';
COMMENT ON COLUMN dbo.setting_management.id IS '流水號';
COMMENT ON COLUMN dbo.setting_management.type IS 'setting';
COMMENT ON COLUMN dbo.setting_management.area_key IS '左邊樹的分類代碼';
COMMENT ON COLUMN dbo.setting_management.area_label IS '左邊樹的分類名稱';
COMMENT ON COLUMN dbo.setting_management.section_key IS '分類底下那一節的代碼';
COMMENT ON COLUMN dbo.setting_management.section_label IS '那一節的名稱';
COMMENT ON COLUMN dbo.setting_management.section_note IS '那一節標題下的說明；沒有就是 NULL';
COMMENT ON COLUMN dbo.setting_management.group_label IS '節裡的小標';
COMMENT ON COLUMN dbo.setting_management.setting_key IS '這一項的代碼，程式照它讀值；按鈕與資訊列沒有';
COMMENT ON COLUMN dbo.setting_management.kind IS 'toggle（開關）／choice（下拉）／info（資訊）／action（按鈕）';
COMMENT ON COLUMN dbo.setting_management.label IS '這一項的名稱';
COMMENT ON COLUMN dbo.setting_management.inline IS '開關右邊那一句';
COMMENT ON COLUMN dbo.setting_management.note IS '名稱下面的說明';
COMMENT ON COLUMN dbo.setting_management.value IS '現在的值';
COMMENT ON COLUMN dbo.setting_management.fallback IS '回復預設時寫回的值';
COMMENT ON COLUMN dbo.setting_management.options IS '下拉的每一個選項，字串陣列';
COMMENT ON COLUMN dbo.setting_management.danger IS '按鈕是不是破壞性的（紅色）';
COMMENT ON COLUMN dbo.setting_management.button IS '按鈕上的字';
COMMENT ON COLUMN dbo.setting_management.sort IS '排列順序';
COMMENT ON COLUMN dbo.setting_management.created_at IS '建立時刻';
COMMENT ON COLUMN dbo.setting_management.updated_at IS '最後修改時刻';

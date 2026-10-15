
-- 設定：一列一項，種類寫在 kind，選項放 jsonb
CREATE TABLE IF NOT EXISTS dbo.setting (
    id             serial PRIMARY KEY,
    type           text    NOT NULL DEFAULT 'setting',
    area_key       text    NOT NULL,
    area_label     text    NOT NULL,
    section_key    text    NOT NULL,
    section_label  text    NOT NULL,
    section_note   text,
    group_label    text    NOT NULL,
    setting_key    text,
    kind           text    NOT NULL,
    label          text    NOT NULL,
    inline         text,
    note           text,
    value          text,
    fallback       text,
    options        jsonb   NOT NULL DEFAULT '[]',
    danger         boolean NOT NULL DEFAULT false,
    button         text,
    sort           integer NOT NULL,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE dbo.setting IS '平台設定：一列一項，種類寫在 kind，選項放 jsonb';
COMMENT ON COLUMN dbo.setting.id IS '流水號';
COMMENT ON COLUMN dbo.setting.type IS 'setting';
COMMENT ON COLUMN dbo.setting.area_key IS '左邊樹的分類代碼';
COMMENT ON COLUMN dbo.setting.area_label IS '左邊樹的分類名稱';
COMMENT ON COLUMN dbo.setting.section_key IS '分類底下那一節的代碼';
COMMENT ON COLUMN dbo.setting.section_label IS '那一節的名稱';
COMMENT ON COLUMN dbo.setting.section_note IS '那一節標題下的說明；沒有就是 NULL';
COMMENT ON COLUMN dbo.setting.group_label IS '節裡的小標';
COMMENT ON COLUMN dbo.setting.setting_key IS '這一項的代碼，程式照它讀值；按鈕與資訊列沒有';
COMMENT ON COLUMN dbo.setting.kind IS 'toggle（開關）／choice（下拉）／info（資訊）／action（按鈕）';
COMMENT ON COLUMN dbo.setting.label IS '這一項的名稱';
COMMENT ON COLUMN dbo.setting.inline IS '開關右邊那一句';
COMMENT ON COLUMN dbo.setting.note IS '名稱下面的說明';
COMMENT ON COLUMN dbo.setting.value IS '現在的值';
COMMENT ON COLUMN dbo.setting.fallback IS '回復預設時寫回的值';
COMMENT ON COLUMN dbo.setting.options IS '下拉的每一個選項，字串陣列';
COMMENT ON COLUMN dbo.setting.danger IS '按鈕是不是破壞性的（紅色）';
COMMENT ON COLUMN dbo.setting.button IS '按鈕上的字';
COMMENT ON COLUMN dbo.setting.sort IS '排列順序';
COMMENT ON COLUMN dbo.setting.created_at IS '建立時刻';
COMMENT ON COLUMN dbo.setting.updated_at IS '最後修改時刻';

-- 設定的每一項
INSERT INTO dbo.setting (area_key, area_label, section_key, section_label, section_note, group_label, setting_key, kind, label, inline, note, value, fallback, options, danger, sort) VALUES
  ('device', '這台裝置', 'general', '一般', '這台裝置的啟動與登入行為，變更在下次開機生效。', '啟動', 'launchOnBoot', 'toggle', '開機時自動啟動', '交接班不必手動開啟', NULL, 'true', 'true', '[]'::jsonb, false, 6),
  ('device', '這台裝置', 'update', '工具套件', NULL, '套件庫', 'source', 'choice', '套件庫資料夾', NULL, '平台從這個資料夾讀可以匯入的工具套件。廠區離線部署，不會連到外網。', NULL, NULL, '[]'::jsonb, false, 10),
  ('device', '這台裝置', 'notify', '通知', NULL, '提醒', 'notifySound', 'toggle', '通知音效', '廠區噪音大時通常關閉', NULL, 'false', 'false', '[]'::jsonb, false, 15),
  ('work', '工作方式', 'floor', '現場操作與顯示', '針對戴手套、站著操作、廠區光線強的環境調整。', '操作', 'bigTargets', 'toggle', '大按鈕模式', '按鈕與輸入框加高到 44px 以上', NULL, 'false', 'false', '[]'::jsonb, false, 21),
  ('work', '工作方式', 'floor', '現場操作與顯示', '針對戴手套、站著操作、廠區光線強的環境調整。', '操作', 'textScale', 'choice', '文字大小', NULL, '站在機台旁看螢幕時可以放大。', '標準 100%', '標準 100%', '["標準 100%","放大 115%","放大 130%"]'::jsonb, false, 22),
  ('work', '工作方式', 'floor', '現場操作與顯示', '針對戴手套、站著操作、廠區光線強的環境調整。', '操作', 'screenAwake', 'toggle', '使用中保持螢幕不休眠', '作業中不會自動鎖定', NULL, 'true', 'true', '[]'::jsonb, false, 23),
  ('work', '工作方式', 'floor', '現場操作與顯示', '針對戴手套、站著操作、廠區光線強的環境調整。', '顯示', 'highContrast', 'toggle', '高對比顯示', '日照強的區域建議開啟', NULL, 'false', 'false', '[]'::jsonb, false, 24),
  ('support', '支援', 'about', '關於本軟體', NULL, '支援', NULL, 'action', '回復成資訊部建議設定', NULL, '只重設這台裝置上的偏好，不影響已安裝的工具。', NULL, NULL, '[]'::jsonb, true, 33);
UPDATE dbo.setting SET button = '回復預設' WHERE sort = 33;


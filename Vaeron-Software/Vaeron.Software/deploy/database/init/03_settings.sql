-- 平台設定：只存值。區段、標籤、說明、選項的字是設計圖上固定的字，寫在設定頁的 XAML
-- value 是現在的值、fallback 是資訊部建議的值（「回復成資訊部建議設定」照它改回去）；一項設定各一列

CREATE TABLE dbo.setting (
    id           serial PRIMARY KEY,
    type         character varying(8) NOT NULL,
    setting_key  character varying(14) NOT NULL,
    enabled      boolean,
    number       integer,
    path         character varying(32767),
    created_at   timestamp with time zone NOT NULL DEFAULT now(),
    updated_at   timestamp with time zone NOT NULL DEFAULT now()
);

-- 同一項設定的現在值、建議值各只有一列
CREATE UNIQUE INDEX setting_key ON dbo.setting (type, setting_key);

COMMENT ON TABLE dbo.setting IS '平台設定的值：value 現在的值、fallback 資訊部建議的值；每一項是開關、數字或路徑其中一種，只填那一欄';
COMMENT ON COLUMN dbo.setting.id IS '流水號';
COMMENT ON COLUMN dbo.setting.type IS 'value＝現在的值；fallback＝資訊部建議的值';
COMMENT ON COLUMN dbo.setting.setting_key IS 'launchOnBoot 開機時自動啟動、source 套件庫資料夾、notifySound 通知音效、bigTargets 大按鈕模式、textScale 文字大小、screenAwake 使用中保持螢幕不休眠、highContrast 高對比顯示、toolKeyMinutes 工具設定解鎖多久；最長的 toolKeyMinutes 是 14 個字';
COMMENT ON COLUMN dbo.setting.enabled IS '開關類（launchOnBoot、notifySound、bigTargets、screenAwake、highContrast）：開或關';
COMMENT ON COLUMN dbo.setting.number IS '數字類：textScale 是百分比（100、115、130），toolKeyMinutes 是分鐘';
COMMENT ON COLUMN dbo.setting.path IS '路徑類（source）；還沒有值時平台啟動時照這台電腦這個帳號的位置填上';
COMMENT ON COLUMN dbo.setting.created_at IS '這一列建立的時刻';
COMMENT ON COLUMN dbo.setting.updated_at IS '最後一次改這個值的時刻';

-- 每一項的現在值與建議值
INSERT INTO dbo.setting (type, setting_key, enabled, number, path)
SELECT t.type, v.setting_key, v.enabled, v.number, v.path
  FROM (VALUES ('value'), ('fallback')) AS t(type)
 CROSS JOIN (VALUES
        ('launchOnBoot', true, NULL::integer, NULL::character varying(32767)),
        ('source', NULL::boolean, NULL, NULL),
        ('notifySound', false, NULL, NULL),
        ('bigTargets', false, NULL, NULL),
        ('textScale', NULL, 100, NULL),
        ('screenAwake', true, NULL, NULL),
        ('highContrast', false, NULL, NULL),
        ('toolKeyMinutes', NULL, 5, NULL)
       ) AS v(setting_key, enabled, number, path);

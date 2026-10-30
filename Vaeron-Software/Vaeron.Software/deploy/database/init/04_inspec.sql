-- InSpecTool：設定、欄位、每一次執行；一列是什麼由 type 決定。藍圖 .claude/blueprint/藍圖_InSpecTool.md

-- 格式化時刻用的時區：跟這台電腦一樣（台北），不是容器預設的 UTC
ALTER DATABASE helix_studio SET timezone TO 'Asia/Taipei';

CREATE TABLE dbo.inspec (
    id          serial PRIMARY KEY,
    type        character varying(7) NOT NULL,
    setting_key character varying(12),
    enabled     boolean,
    number      numeric,
    path        character varying(32767),
    name        character varying(50),
    prompt      character varying(255),
    mode        character varying(7),
    editable    boolean,
    sort        integer,
    program     character varying(32767),
    fields      jsonb NOT NULL DEFAULT '{}',
    prompts     jsonb NOT NULL DEFAULT '[]',
    started_at  timestamp with time zone,
    finished_at timestamp with time zone,
    exit_code   integer,
    export      text,
    archive     character varying(32767),
    note        text,
    removed_at  timestamp with time zone,
    created_at  timestamp with time zone NOT NULL DEFAULT now(),
    updated_at  timestamp with time zone NOT NULL DEFAULT now()
);

-- 每一項設定只有一列
CREATE UNIQUE INDEX inspec_setting ON dbo.inspec (setting_key) WHERE type = 'setting';

COMMENT ON TABLE dbo.inspec IS 'InSpecTool：setting 一項設定、field 一個欄位（移除寫 removed_at）、run 一次執行，一列是什麼由 type 決定';
COMMENT ON COLUMN dbo.inspec.id IS '流水號';
COMMENT ON COLUMN dbo.inspec.type IS 'setting＝一項設定；field＝一個欄位；run＝一次執行';
COMMENT ON COLUMN dbo.inspec.setting_key IS '[setting] searchFolder 搜尋資料夾、logFolder 紀錄資料夾、backupFolder 備份路徑、scanInterval 條碼解析間隔、showResult 跑完顯示結果、leftWidth 左欄寬；最長 12 個字';
COMMENT ON COLUMN dbo.inspec.enabled IS '[setting] 開關類（showResult）';
COMMENT ON COLUMN dbo.inspec.number IS '[setting] 數字類：scanInterval 毫秒、leftWidth px（沒拖過是空的）';
COMMENT ON COLUMN dbo.inspec.path IS '[setting] 路徑類（searchFolder、logFolder、backupFolder）；還沒選是空的';
COMMENT ON COLUMN dbo.inspec.name IS '[field] 欄位名稱；設定頁的輸入框 MaxLength 也是 50';
COMMENT ON COLUMN dbo.inspec.prompt IS '[field] 對應的 InSpec 提示文字，完全相同（不分大小寫）才填；設定頁的輸入框 MaxLength 也是 255';
COMMENT ON COLUMN dbo.inspec.mode IS '[field] fill＝直接填；confirm＝先確認再填';
COMMENT ON COLUMN dbo.inspec.editable IS '[field] 主頁上可否改';
COMMENT ON COLUMN dbo.inspec.sort IS '[field] 欄位順序，也是條碼分段的順序';
COMMENT ON COLUMN dbo.inspec.program IS '[run] 執行的 .iwp 原檔完整路徑';
COMMENT ON COLUMN dbo.inspec.fields IS '[run] 這次執行的欄位值：欄位名稱 → 值';
COMMENT ON COLUMN dbo.inspec.prompts IS '[run] 每個提示怎麼處理：prompt、value、at';
COMMENT ON COLUMN dbo.inspec.started_at IS '[run] 按下執行的時刻';
COMMENT ON COLUMN dbo.inspec.finished_at IS '[run] iscmd 回來的時刻（InSpec 跑完）';
COMMENT ON COLUMN dbo.inspec.exit_code IS '[run] iscmd 回傳代碼';
COMMENT ON COLUMN dbo.inspec.export IS '[run] 這次匯出檔的內容';
COMMENT ON COLUMN dbo.inspec.archive IS '[run] 實際寫入的歸檔檔路徑；沒歸檔是空的';
COMMENT ON COLUMN dbo.inspec.note IS '[run] 沒歸檔或出錯時的原因';
COMMENT ON COLUMN dbo.inspec.removed_at IS '[field] 移除的時刻；移除是狀態，列不真的刪';
COMMENT ON COLUMN dbo.inspec.created_at IS '這一列建立的時刻';
COMMENT ON COLUMN dbo.inspec.updated_at IS '這一列最後修改的時刻';

-- 設定的每一項：路徑還沒選，解析間隔 80 毫秒，跑完顯示結果
INSERT INTO dbo.inspec (type, setting_key, enabled, number, path) VALUES
    ('setting', 'searchFolder', NULL, NULL, NULL),
    ('setting', 'logFolder', NULL, NULL, NULL),
    ('setting', 'backupFolder', NULL, NULL, NULL),
    ('setting', 'scanInterval', NULL, 80, NULL),
    ('setting', 'showResult', true, NULL, NULL),
    ('setting', 'leftWidth', NULL, NULL, NULL);

-- 預設欄位（舊版 7/17 的五個）
INSERT INTO dbo.inspec (type, name, prompt, mode, editable, sort) VALUES
    ('field', '料號', 'part number', 'fill', true, 1),
    ('field', '批號', 'lot number', 'fill', true, 2),
    ('field', '壓合', 'press', 'confirm', false, 3),
    ('field', '機台', 'machine', 'fill', true, 4),
    ('field', '操作員', 'operator', 'fill', true, 5);

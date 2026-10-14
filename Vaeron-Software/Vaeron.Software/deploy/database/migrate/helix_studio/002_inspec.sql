-- InSpecTool：設定、欄位、每一次執行
-- 一列是什麼由 type 決定；藍圖 .claude/blueprint/藍圖_InSpecTool.md

-- view 格式化時刻用的時區：跟這台電腦一樣（台北），不是容器預設的 UTC
ALTER DATABASE helix_studio SET timezone TO 'Asia/Taipei';

CREATE TABLE IF NOT EXISTS dbo.inspec_management (
    id          serial PRIMARY KEY,
    type        text        NOT NULL,                  -- setting / field / run
    setting_key text,                                  -- [setting] searchFolder / logFolder / backupFolder / scanInterval / showResult / unlockCode
    value       text,                                  -- [setting] 設定值
    options     jsonb       NOT NULL DEFAULT '[]',     -- [setting] 可選的值
    name        text,                                  -- [field] 欄位名稱
    prompt      text,                                  -- [field] 對應的 InSpec 提示文字，完全相同（不分大小寫）才填
    mode        text,                                  -- [field] fill（直接填）/ confirm（先確認再填）
    editable    boolean,                               -- [field] 主頁上可否改
    sort        integer,                               -- [field] 欄位順序，也是條碼分段的順序
    program     text,                                  -- [run] 執行的 .iwp 原檔完整路徑
    fields      jsonb       NOT NULL DEFAULT '{}',     -- [run] 這次執行的欄位值
    prompts     jsonb       NOT NULL DEFAULT '[]',     -- [run] 每個提示怎麼處理：prompt / value / at
    started_at  timestamptz,                           -- [run] 按下執行的時刻
    finished_at timestamptz,                           -- [run] iscmd 回來的時刻（InSpec 跑完）
    exit_code   integer,                               -- [run] iscmd 回傳代碼
    export      text,                                  -- [run] 這次匯出檔的內容
    archive     text,                                  -- [run] 實際寫入的歸檔檔路徑
    note        text,                                  -- [run] 沒歸檔或出錯時的原因
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

-- InSpecTool 這一個讀取者一支：耗時、時刻格式、排序由 view 做完
CREATE OR REPLACE VIEW dbo.vw_inspec AS
SELECT i.id, i.type, i.setting_key, i.value, i.options,
       i.name, i.prompt, i.mode, i.editable, i.sort,
       i.program, i.fields, i.prompts, i.started_at, i.finished_at, i.exit_code, i.export, i.archive, i.note,
       (EXTRACT(EPOCH FROM (i.finished_at - i.started_at)) * 1000)::bigint AS elapsed_ms,
       to_char(i.started_at, 'YYYY-MM-DD HH24:MI:SS') AS started_text,
       i.created_at, i.updated_at
  FROM dbo.inspec_management AS i
 ORDER BY i.type, i.sort NULLS LAST, i.started_at DESC NULLS LAST, i.id;

-- 設定的每一項
INSERT INTO dbo.inspec_management (type, setting_key, value, options)
SELECT v.type, v.setting_key, v.value, v.options::jsonb
  FROM (VALUES
        ('setting', 'searchFolder', '', '[]'),
        ('setting', 'logFolder', '', '[]'),
        ('setting', 'backupFolder', '', '[]'),
        ('setting', 'scanInterval', '80', '["80","120","160","200","300","500"]'),
        ('setting', 'showResult', 'true', '[]'),
        ('setting', 'unlockCode', '123', '[]')
       ) AS v(type, setting_key, value, options)
 WHERE NOT EXISTS (SELECT 1 FROM dbo.inspec_management AS i WHERE i.type = 'setting' AND i.setting_key = v.setting_key);

-- 預設欄位（舊版 7/17 的五個）
INSERT INTO dbo.inspec_management (type, name, prompt, mode, editable, sort)
SELECT v.type, v.name, v.prompt, v.mode, v.editable, v.sort
  FROM (VALUES
        ('field', '料號', 'part number', 'fill', true, 1),
        ('field', '批號', 'lot number', 'fill', true, 2),
        ('field', '壓合', 'press', 'confirm', false, 3),
        ('field', '機台', 'machine', 'fill', true, 4),
        ('field', '操作員', 'operator', 'fill', true, 5)
       ) AS v(type, name, prompt, mode, editable, sort)
 WHERE NOT EXISTS (SELECT 1 FROM dbo.inspec_management AS i WHERE i.type = 'field');

-- 表與每一欄的說明
COMMENT ON TABLE dbo.inspec_management IS 'InSpecTool：設定、欄位、每一次執行，一列是什麼由 type 決定';
COMMENT ON COLUMN dbo.inspec_management.id IS '流水號';
COMMENT ON COLUMN dbo.inspec_management.type IS 'setting 一項設定／field 一個欄位／run 一次執行';
COMMENT ON COLUMN dbo.inspec_management.setting_key IS '[setting] 哪一項：searchFolder、logFolder、backupFolder、scanInterval、showResult、unlockCode';
COMMENT ON COLUMN dbo.inspec_management.value IS '[setting] 設定值';
COMMENT ON COLUMN dbo.inspec_management.options IS '[setting] 可選的值';
COMMENT ON COLUMN dbo.inspec_management.name IS '[field] 欄位名稱';
COMMENT ON COLUMN dbo.inspec_management.prompt IS '[field] 對應的 InSpec 提示文字，完全相同（不分大小寫）才填';
COMMENT ON COLUMN dbo.inspec_management.mode IS '[field] fill 直接填／confirm 先確認再填';
COMMENT ON COLUMN dbo.inspec_management.editable IS '[field] 主頁上可否改';
COMMENT ON COLUMN dbo.inspec_management.sort IS '[field] 欄位順序，也是條碼分段的順序';
COMMENT ON COLUMN dbo.inspec_management.program IS '[run] 執行的量測程式完整路徑';
COMMENT ON COLUMN dbo.inspec_management.fields IS '[run] 這次執行的欄位值';
COMMENT ON COLUMN dbo.inspec_management.prompts IS '[run] 每個提示怎麼處理：prompt、value、at';
COMMENT ON COLUMN dbo.inspec_management.started_at IS '[run] 按下執行的時刻；InSpec 自己寫入的是寫入的時刻';
COMMENT ON COLUMN dbo.inspec_management.finished_at IS '[run] iscmd 回來的時刻，也就是 InSpec 跑完';
COMMENT ON COLUMN dbo.inspec_management.exit_code IS '[run] iscmd 回傳代碼';
COMMENT ON COLUMN dbo.inspec_management.export IS '[run] InSpec 寫出的內容';
COMMENT ON COLUMN dbo.inspec_management.archive IS '[run] 實際寫入的歸檔檔路徑';
COMMENT ON COLUMN dbo.inspec_management.note IS '[run] 沒歸檔或出錯時的原因';
COMMENT ON COLUMN dbo.inspec_management.created_at IS '這一列建立的時刻';
COMMENT ON COLUMN dbo.inspec_management.updated_at IS '這一列最後修改的時刻';

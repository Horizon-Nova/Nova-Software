-- 工具金鑰併進 tool：同一種東西（工具）的不同種類用 type 區分，不另開表
--   tool 一支工具、key 一把工具金鑰、use 用了一次；tool_key 拿掉

ALTER TABLE dbo.tool RENAME TO tool_old;
ALTER SEQUENCE dbo.tool_id_seq RENAME TO tool_old_id_seq;
ALTER TABLE dbo.tool_old RENAME CONSTRAINT tool_pkey TO tool_old_pkey;

CREATE TABLE dbo.tool (
    id           serial PRIMARY KEY,
    type         text NOT NULL,                       -- tool / key / use
    tool_no      text NOT NULL,                       -- 工具代碼
    name         text,                                -- [tool] 工具名稱
    icon         text,                                -- [tool] lucide 的圖示名稱
    cat          text,                                -- [tool] 分類
    vendor       text,                                -- [tool] 維護團隊
    version      text,                                -- [tool] 裝著的版本
    size         text,                                -- [tool] 套件大小
    updated      date,                                -- [tool] 匯入這一版的日期
    description  text,                                -- [tool] 說明
    capabilities jsonb NOT NULL DEFAULT '[]',         -- [tool] 這支工具會做的事
    permissions  jsonb NOT NULL DEFAULT '[]',         -- [tool] 會用到的權限
    changelog    jsonb NOT NULL DEFAULT '[]',         -- [tool] 版本與更新說明，一則一個元素，新到舊
    entry        text,                                -- [tool] 開起來要載入的組件檔名
    class        text,                                -- [tool] 組件裡那個畫面的型別全名
    home         text,                                -- [tool] 這支工具的檔案裝在哪一個資料夾
    source       text,                                -- [tool] 從哪裡來
    settings     text,                                -- [tool] 組件裡設定畫面的型別全名；沒有就是這支工具沒有設定
    code         text,                                -- [key] 操作員貼進來的那一串工具金鑰
    action       text,                                -- [use] activate（啟用）／open（開啟）／unlock（解鎖設定）
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE dbo.tool IS '工具：一列是什麼由 type 決定。tool 是匯入的一支工具（匯入就是裝好，移除就是整列拿掉）；key 是啟用時存下的工具金鑰，最新的那一把就是現在用的；use 是每一次啟用、開啟、解鎖設定';
COMMENT ON COLUMN dbo.tool.id IS '流水號，也是側欄與工具目錄的先後';
COMMENT ON COLUMN dbo.tool.type IS 'tool 一支工具／key 一把工具金鑰／use 用了一次';
COMMENT ON COLUMN dbo.tool.tool_no IS '工具代碼，套件 manifest 的 tool_no；key、use 用它對到那一支工具';
COMMENT ON COLUMN dbo.tool.name IS '[tool] 工具名稱';
COMMENT ON COLUMN dbo.tool.icon IS '[tool] lucide 的圖示名稱';
COMMENT ON COLUMN dbo.tool.cat IS '[tool] 分類';
COMMENT ON COLUMN dbo.tool.vendor IS '[tool] 維護團隊';
COMMENT ON COLUMN dbo.tool.version IS '[tool] 裝著的版本';
COMMENT ON COLUMN dbo.tool.size IS '[tool] 套件大小，例如 42.8 KB';
COMMENT ON COLUMN dbo.tool.updated IS '[tool] 匯入這一版的日期';
COMMENT ON COLUMN dbo.tool.description IS '[tool] 說明';
COMMENT ON COLUMN dbo.tool.capabilities IS '[tool] 這支工具會做的事，字串陣列';
COMMENT ON COLUMN dbo.tool.permissions IS '[tool] 會用到的權限，字串陣列';
COMMENT ON COLUMN dbo.tool.changelog IS '[tool] 版本與更新說明，一則一個元素（ver、date、note），新到舊';
COMMENT ON COLUMN dbo.tool.entry IS '[tool] 開起來要載入的組件檔名，例如 InSpecTool.dll';
COMMENT ON COLUMN dbo.tool.class IS '[tool] 組件裡那個畫面的型別全名，例如 InSpecTool.Measure';
COMMENT ON COLUMN dbo.tool.home IS '[tool] 這支工具的檔案裝在哪一個資料夾';
COMMENT ON COLUMN dbo.tool.source IS '[tool] 從哪裡來，例如 本機套件檔';
COMMENT ON COLUMN dbo.tool.settings IS '[tool] 組件裡設定畫面的型別全名，例如 InSpecTool.Setup；沒有就是這支工具沒有設定';
COMMENT ON COLUMN dbo.tool.code IS '[key] 操作員貼進來的那一串工具金鑰；每次開工具都重驗一次';
COMMENT ON COLUMN dbo.tool.action IS '[use] activate（啟用）／open（開啟）／unlock（解鎖設定）';
COMMENT ON COLUMN dbo.tool.created_at IS '寫入時刻：tool 是第一次匯入、key 是貼進來、use 是用的那一刻';
COMMENT ON COLUMN dbo.tool.updated_at IS '最後修改時刻：tool 是最後一次匯入';

-- 工具照原本的 id 放回去，側欄與目錄的先後不變
INSERT INTO dbo.tool (id, type, tool_no, name, icon, cat, vendor, version, size, updated, description, capabilities, permissions, changelog, entry, class, home, source, settings, created_at, updated_at)
SELECT id, 'tool', tool_no, name, icon, cat, vendor, version, size, updated, description, capabilities, permissions, changelog, entry, class, home, source, settings, created_at, updated_at FROM dbo.tool_old;
SELECT setval('dbo.tool_id_seq', (SELECT max(id) FROM dbo.tool));

-- 金鑰與每一次使用，照發生的先後接在後面
INSERT INTO dbo.tool (type, tool_no, code, action, created_at, updated_at)
SELECT type, tool_no, code, action, created_at, updated_at FROM dbo.tool_key ORDER BY created_at, id;

DROP TABLE dbo.tool_key;
DROP TABLE dbo.tool_old;

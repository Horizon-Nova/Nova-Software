-- HelixStudio：工具與搜尋字
-- 一張表裝一種東西，用它裝的東西命名；同一種東西的不同種類由 type 決定。
-- 藍圖 .claude/blueprint/藍圖_平台資料庫.md

CREATE SCHEMA IF NOT EXISTS dbo;

-- 工具：一列是什麼由 type 決定——tool 一支匯入的工具、key 啟用時存下的工具金鑰；啟用、開啟、解鎖記在 event_log
CREATE TABLE dbo.tool (
    id           serial PRIMARY KEY,
    type         text NOT NULL,                       -- tool / key
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
    guide        jsonb NOT NULL DEFAULT '[]',         -- [tool] 操作說明，一步一個元素：text、image（套件裡的相對路徑）
    code         text,                                -- [key] 操作員貼進來的那一串工具金鑰
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE dbo.tool IS '工具：一列是什麼由 type 決定。tool 是匯入的一支工具（匯入就是裝好，移除就是整列拿掉）；key 是啟用時存下的工具金鑰，最新的那一把就是現在用的；啟用、開啟、解鎖記在 event_log';
COMMENT ON COLUMN dbo.tool.id IS '流水號，也是側欄與工具目錄的先後';
COMMENT ON COLUMN dbo.tool.type IS 'tool 一支工具／key 一把工具金鑰';
COMMENT ON COLUMN dbo.tool.tool_no IS '工具代碼，套件 manifest 的 tool_no；key 用它對到那一支工具';
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
COMMENT ON COLUMN dbo.tool.guide IS '[tool] 操作說明，一步一個元素（text 這一步做什麼、image 截圖在套件裡的相對路徑，沒有圖就不寫），照步驟順序';
COMMENT ON COLUMN dbo.tool.code IS '[key] 操作員貼進來的那一串工具金鑰；每次開工具都重驗一次';
COMMENT ON COLUMN dbo.tool.created_at IS '寫入時刻：tool 是第一次匯入、key 是貼進來的時刻';
COMMENT ON COLUMN dbo.tool.updated_at IS '最後修改時刻：tool 是最後一次匯入';

-- 搜尋過的字：一次搜尋一列，全部留著；同一個字搜過幾次、最近一次是什麼時候由 view 算
CREATE TABLE dbo.search (
    id          serial PRIMARY KEY,
    type        text        NOT NULL DEFAULT 'recent',  -- recent
    term        text        NOT NULL,                   -- 搜尋的字
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE dbo.search IS '搜尋過的字：一次搜尋一列';
COMMENT ON COLUMN dbo.search.id IS '流水號';
COMMENT ON COLUMN dbo.search.type IS 'recent';
COMMENT ON COLUMN dbo.search.term IS '搜尋的字';
COMMENT ON COLUMN dbo.search.created_at IS '這一次搜尋的時刻';
COMMENT ON COLUMN dbo.search.updated_at IS '最後修改時刻';


-- HelixStudio：工具與搜尋過的字
-- 這份是討論好的結構的對照與備份：安裝檔照它建表；資料庫壞掉時拿它恢復。藍圖 .claude/blueprint/藍圖_資料庫重整.md

CREATE SCHEMA IF NOT EXISTS dbo;

-- 工具：tool 一次匯入、key 一次啟用、start 一次切自動啟動、remove 一次移除；只新增不改寫，同一種東西的不同種類以 type 區分
CREATE TABLE dbo.tool (
    id           serial PRIMARY KEY,
    type         character varying(6) NOT NULL,
    tool_no      character varying(255) NOT NULL,
    name         character varying(50),
    icon         character varying(50),
    cat          character varying(20),
    vendor       character varying(50),
    version      character varying(23),
    bytes        bigint,
    description  character varying(500),
    capabilities character varying(200)[] NOT NULL DEFAULT '{}',
    permissions  character varying(200)[] NOT NULL DEFAULT '{}',
    changelog    jsonb NOT NULL DEFAULT '[]',
    guide        jsonb NOT NULL DEFAULT '[]',
    entry        character varying(255),
    class        character varying(1023),
    settings     character varying(1023),
    home         character varying(32767),
    source       character varying(32767),
    code         text,
    expires_at   timestamp with time zone,
    autostart    boolean,
    created_at   timestamp with time zone NOT NULL DEFAULT now(),
    updated_at   timestamp with time zone NOT NULL DEFAULT now()
);

COMMENT ON TABLE dbo.tool IS '工具，只新增不改寫：tool 一次匯入（同一個代碼最新的那一次是裝著的版本）；key 一次啟用時驗過的工具金鑰，最新的那一把是現在用的；start 一次切自動啟動，最新的那一列是現在的狀態；remove 一次移除，最新一次匯入之後有移除就是沒裝著。開啟、解鎖記在 event_log';
COMMENT ON COLUMN dbo.tool.id IS '流水號；同一個代碼第一次匯入的那一列是側欄的先後';
COMMENT ON COLUMN dbo.tool.type IS 'tool＝一次匯入；key＝一次啟用；start＝一次切自動啟動；remove＝一次移除';
COMMENT ON COLUMN dbo.tool.tool_no IS '工具代碼，套件 manifest 的 tool_no；也是工具資料夾的名稱（tools\{tool_no}\{版本}），所以照 Windows 檔名上限 255';
COMMENT ON COLUMN dbo.tool.name IS '[tool] 工具名稱；manifest 的 name，helixpack 打包時檢查長度';
COMMENT ON COLUMN dbo.tool.icon IS '[tool] lucide 的圖示名稱；manifest 的 icon';
COMMENT ON COLUMN dbo.tool.cat IS '[tool] 分類；manifest 的 cat';
COMMENT ON COLUMN dbo.tool.vendor IS '[tool] 維護團隊；manifest 的 vendor';
COMMENT ON COLUMN dbo.tool.version IS '[tool] 裝著的版本；Windows 檔案版本 a.b.c.d 每段最大 65535，最長 23 個字';
COMMENT ON COLUMN dbo.tool.bytes IS '[tool] 套件檔的位元組數';
COMMENT ON COLUMN dbo.tool.description IS '[tool] 說明；manifest 的 description';
COMMENT ON COLUMN dbo.tool.capabilities IS '[tool] 這支工具會做的事，一條一個元素；manifest 的 capabilities';
COMMENT ON COLUMN dbo.tool.permissions IS '[tool] 會用到的權限，一條一個元素；manifest 的 permissions';
COMMENT ON COLUMN dbo.tool.changelog IS '[tool] 版本與更新說明，一則一個元素（ver、date、note），新到舊';
COMMENT ON COLUMN dbo.tool.guide IS '[tool] 操作說明，一步一個元素（text 這一步做什麼、image 截圖在套件裡的相對路徑，沒有圖就不寫），照步驟順序';
COMMENT ON COLUMN dbo.tool.entry IS '[tool] 開起來要載入的組件檔名';
COMMENT ON COLUMN dbo.tool.class IS '[tool] 組件裡那個畫面的型別全名；.NET 型別全名上限 1023';
COMMENT ON COLUMN dbo.tool.settings IS '[tool] 組件裡設定畫面的型別全名；沒有就是這支工具沒有設定';
COMMENT ON COLUMN dbo.tool.home IS '[tool] 這支工具的檔案裝在哪一個資料夾；Windows 長路徑上限 32767';
COMMENT ON COLUMN dbo.tool.source IS '[tool] 套件檔從哪裡來';
COMMENT ON COLUMN dbo.tool.code IS '[key] 操作員貼進來、驗過的那一串工具金鑰；每次開工具都重驗一次';
COMMENT ON COLUMN dbo.tool.expires_at IS '[key] 這把金鑰到哪一刻為止；啟用時驗過簽章後從金鑰讀出寫進來，沒有到期日是空的';
COMMENT ON COLUMN dbo.tool.autostart IS '[start] 這一次切成自動啟動（true）或取消（false）';
COMMENT ON COLUMN dbo.tool.created_at IS '這一列建立的時刻：[tool] 匯入、[key] 啟用、[start] 切換、[remove] 移除的時刻';
COMMENT ON COLUMN dbo.tool.updated_at IS '這一列最後修改的時刻（只新增不改寫，等於建立的時刻）';

-- 搜尋過的字：一次搜尋一列，全部留著；同一個字搜過幾次、最近一次是什麼時候由 view 算
CREATE TABLE dbo.search (
    id          serial PRIMARY KEY,
    type        character varying(6) NOT NULL DEFAULT 'recent',
    term        character varying(100) NOT NULL,
    created_at  timestamp with time zone NOT NULL DEFAULT now(),
    updated_at  timestamp with time zone NOT NULL DEFAULT now()
);

COMMENT ON TABLE dbo.search IS '搜尋過的字：一次搜尋一列，全部留著';
COMMENT ON COLUMN dbo.search.id IS '流水號';
COMMENT ON COLUMN dbo.search.type IS 'recent＝一次搜尋';
COMMENT ON COLUMN dbo.search.term IS '搜尋的字；搜尋框的 MaxLength 也是 100';
COMMENT ON COLUMN dbo.search.created_at IS '搜尋的時刻';
COMMENT ON COLUMN dbo.search.updated_at IS '這一列最後修改的時刻';

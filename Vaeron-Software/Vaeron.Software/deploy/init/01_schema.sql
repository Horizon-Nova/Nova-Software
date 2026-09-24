-- HelixStudio：工具與裝置
-- 一個管理範圍一張表；一列是什麼由 type 決定；附屬紀錄留在 jsonb 欄。

CREATE SCHEMA IF NOT EXISTS dbo;

-- 工具管理：套件庫裡的每一支工具
CREATE TABLE dbo.tool_management (
    id SERIAL,
    type text NOT NULL,                              -- tool
    tool_no text NOT NULL,                           -- 工具代碼，設計檔的 k
    name text NOT NULL,
    icon text NOT NULL,                              -- lucide 的圖示名稱
    cat text NOT NULL,                               -- 分類
    vendor text NOT NULL,                            -- 維護團隊
    version text NOT NULL,                           -- 套件庫上的版本
    installed_version text,                          -- 這台裝置上的版本；沒裝就沒有
    state text NOT NULL,                             -- installed / update / not_installed / required_missing
    required boolean NOT NULL,                       -- IT 強制安裝
    pinned boolean NOT NULL,                         -- 釘選在側邊導覽列
    badge integer NOT NULL,                          -- 側邊導覽列上的未讀數
    auto_update boolean NOT NULL,
    audience text NOT NULL,                          -- 給誰用
    size text NOT NULL,                              -- 套件大小
    updated date NOT NULL,                           -- 套件庫上的更新日期
    description text NOT NULL,
    guide jsonb,                                       -- 怎麼用
    seats jsonb NOT NULL DEFAULT '{}',               -- 席次：used / total
    capabilities jsonb NOT NULL DEFAULT '[]',        -- 這支工具會做的事
    permissions jsonb NOT NULL DEFAULT '[]',         -- 會用到的權限
    changelog jsonb NOT NULL DEFAULT '[]',           -- 版本與更新說明，一則一個元素
    usage jsonb NOT NULL DEFAULT '[]',               -- 近 30 天的使用量，一天一個元素
    entry text,                                      -- 開起來要載入的組件檔名，例如 ChecksumTool.dll
    class text,                                      -- 組件裡那個畫面的型別全名，例如 ChecksumTool.Board
    home text,                                       -- 這支工具的檔案裝在哪一個資料夾
    source text,                                     -- 從哪裡來：本機套件檔、公司套件庫、隨平台內建
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT tool_management_pkey PRIMARY KEY (id)
);

-- 裝置管理：這台裝置，與用這台裝置的人
CREATE TABLE dbo.device_management (
    id SERIAL,
    type text NOT NULL,                              -- device / person
    host text,                                       -- [device] 電腦名稱
    os text,                                         -- [device] 作業系統
    ip text,                                         -- [device] 內網 IP
    client text,                                     -- [device] 用戶端版本
    build text,                                      -- [device] 組建編號
    sync_at text,                                    -- [device] 套件庫最後一次匯入
    name text,                                       -- [person] 姓名
    dept text,                                       -- [person] 部門與職稱
    roles jsonb NOT NULL DEFAULT '[]',               -- [person] 身上的角色
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT device_management_pkey PRIMARY KEY (id)
);


-- 搜尋過的字：一列一個字，最近用到的時刻決定它排在前面
CREATE TABLE IF NOT EXISTS dbo.search_management (
    id      serial PRIMARY KEY,
    type    text        NOT NULL DEFAULT 'recent',
    term    text        NOT NULL UNIQUE,
    used_at timestamptz NOT NULL DEFAULT now()
);

-- 桌面用戶端這一個讀取者一支：工具、裝置、人併成一支，以 type 區分
CREATE OR REPLACE VIEW dbo.vw_shell AS
SELECT 'tool'::text AS type,
       t.id,
       t.tool_no,
       t.name,
       t.icon,
       t.cat,
       t.vendor,
       t.version,
       t.installed_version,
       t.state,
       t.required,
       t.pinned,
       t.badge,
       t.auto_update,
       t.audience,
       t.size,
       t.updated,
       t.description,
       t.guide,
       t.seats,
       t.capabilities,
       t.permissions,
       t.changelog,
       t.usage,
       t.entry,
       t.class,
       t.home,
       t.source,
       NULL::text AS host,
       NULL::text AS os,
       NULL::text AS ip,
       NULL::text AS client,
       NULL::text AS build,
       NULL::text AS sync_at,
       NULL::text AS dept,
       '[]'::jsonb AS roles,
       NULL::timestamptz AS used_at
  FROM dbo.tool_management AS t
 WHERE t.type = 'tool'
UNION ALL
SELECT d.type,
       d.id,
       NULL,
       d.name,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       '{}'::jsonb,
       '[]'::jsonb,
       '[]'::jsonb,
       '[]'::jsonb,
       '[]'::jsonb,
       NULL,
       NULL,
       NULL,
       NULL,
       d.host,
       d.os,
       d.ip,
       d.client,
       d.build,
       d.sync_at,
       d.dept,
       d.roles,
       NULL::timestamptz
  FROM dbo.device_management AS d
UNION ALL
SELECT 'search'::text,
       s.id,
       NULL,
       s.term,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       '{}'::jsonb,
       '[]'::jsonb,
       '[]'::jsonb,
       '[]'::jsonb,
       '[]'::jsonb,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       '[]'::jsonb,
       s.used_at
  FROM dbo.search_management AS s
 ORDER BY type, used_at DESC NULLS LAST, id;

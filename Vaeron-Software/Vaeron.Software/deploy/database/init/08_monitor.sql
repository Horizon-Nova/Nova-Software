-- 程式監控：一件事一種 type（閃退、無回應、結束…），見過的程式與開機自動執行、欄位設定也在同一張；存值，不存寫好的句子
-- 藍圖 .claude/blueprint/藍圖_程式監控.md、藍圖_資料庫重整.md

CREATE TABLE dbo.monitor (
    id            serial PRIMARY KEY,
    type          character varying(8) NOT NULL,
    exe_path      character varying(32767),
    name          text,
    happened_at   timestamp with time zone,
    record_id     bigint,
    code          character varying(10),
    module        character varying(255),
    seconds       integer,
    priority_from integer,
    priority_to   integer,
    other_path    character varying(32767),
    up_kbps       integer,
    cpu_pct       integer,
    mem_mb        integer,
    block         boolean,
    protocol      character varying(3),
    address       character varying(53),
    source        character varying(7),
    location      character varying(32767),
    columns       character varying(8)[],
    created_at    timestamp with time zone NOT NULL DEFAULT now(),
    updated_at    timestamp with time zone NOT NULL DEFAULT now()
);

-- 同一支程式只記一次見過；同一個開機自動執行只記一次；Windows 事件記錄的同一筆只補一次；欄位設定只有一列
CREATE UNIQUE INDEX monitor_seen ON dbo.monitor (lower(exe_path)) WHERE type = 'seen';
CREATE UNIQUE INDEX monitor_startup ON dbo.monitor (lower(exe_path), source, location) WHERE type = 'startup';
CREATE UNIQUE INDEX monitor_record ON dbo.monitor (record_id) WHERE record_id IS NOT NULL;
CREATE UNIQUE INDEX monitor_columns ON dbo.monitor (type) WHERE type = 'columns';

COMMENT ON TABLE dbo.monitor IS '程式監控：一件事一列（crash、hang、end、priority、suspend、resume、ease、unease、limit、first、autorun、listen），seen 見過的程式、startup 見過的開機自動執行、columns 排成欄的量測項目；一支程式現在的限制是它最新的那一列 limit';
COMMENT ON COLUMN dbo.monitor.id IS '流水號';
COMMENT ON COLUMN dbo.monitor.type IS 'crash＝閃退；hang＝沒有回應；end＝在這台按住結束；priority＝改優先順序；suspend＝暫停；resume＝繼續；ease＝緩和；unease＝取消緩和；limit＝設一次限制；first＝第一次出現；autorun＝新增開機自動執行；listen＝開始聽一個埠；seen＝見過的程式；startup＝見過的開機自動執行；columns＝排成欄的量測項目';
COMMENT ON COLUMN dbo.monitor.exe_path IS '程式的完整路徑，照 Windows 給的大小寫，比對時不分大小寫；.NET 閃退的記錄只有檔名時就是檔名';
COMMENT ON COLUMN dbo.monitor.name IS '[事件] 程式名稱：它自己的檔案描述，沒有就是檔名；外部給的，沒有上限';
COMMENT ON COLUMN dbo.monitor.happened_at IS '[事件] 發生的時刻；從 Windows 事件記錄補進來的照它記的時刻';
COMMENT ON COLUMN dbo.monitor.record_id IS '[crash][hang] Windows 事件記錄的編號；從那裡補進來的才有';
COMMENT ON COLUMN dbo.monitor.code IS '[crash] 例外碼，例如 0xc0000005';
COMMENT ON COLUMN dbo.monitor.module IS '[crash] 出錯的模組檔名';
COMMENT ON COLUMN dbo.monitor.seconds IS '[hang] 沒有回應幾秒';
COMMENT ON COLUMN dbo.monitor.priority_from IS '[priority][ease][unease] 改之前的 Windows 優先順序類別值';
COMMENT ON COLUMN dbo.monitor.priority_to IS '[priority][ease][unease] 改之後的 Windows 優先順序類別值';
COMMENT ON COLUMN dbo.monitor.other_path IS '[ease][unease] 被暫停、被繼續的那一支程式的路徑';
COMMENT ON COLUMN dbo.monitor.up_kbps IS '[limit] 上傳上限 KB/s；空的是不限';
COMMENT ON COLUMN dbo.monitor.cpu_pct IS '[limit] CPU 上限 %；空的是不限';
COMMENT ON COLUMN dbo.monitor.mem_mb IS '[limit] 記憶體上限 MB；空的是不限';
COMMENT ON COLUMN dbo.monitor.block IS '[limit] 不准連網路';
COMMENT ON COLUMN dbo.monitor.protocol IS '[listen] TCP 或 UDP';
COMMENT ON COLUMN dbo.monitor.address IS '[listen] 聽的位址與埠；[IPv6]:埠 最長 53';
COMMENT ON COLUMN dbo.monitor.source IS '[startup][autorun] 從哪裡自動執行：run 登錄檔 Run、runonce 登錄檔 RunOnce、folder 啟動資料夾、task 排程工作、service 服務';
COMMENT ON COLUMN dbo.monitor.location IS '[startup][autorun] 在那裡的哪一項：登錄檔的機碼與值名稱、啟動資料夾的檔名、排程工作的路徑、服務名稱';
COMMENT ON COLUMN dbo.monitor.columns IS '[columns] 排成欄的量測項目代碼，照排列的先後';
COMMENT ON COLUMN dbo.monitor.created_at IS '這一列建立的時刻';
COMMENT ON COLUMN dbo.monitor.updated_at IS '這一列最後修改的時刻';

-- 程式監控：event 一件事一列（新的在前）；limit 一支程式現在的限制（它最新的那一列 limit）；crashes 一支程式閃退幾次；
-- seen、startup 見過的程式與開機自動執行；columns 排成欄的量測項目；since 最早一筆的時刻
CREATE VIEW dbo.vw_monitor AS
WITH latest AS (
    SELECT DISTINCT ON (lower(l.exe_path)) l.*
      FROM dbo.monitor AS l
     WHERE l.type = 'limit'
     ORDER BY lower(l.exe_path), l.created_at DESC, l.id DESC)
SELECT 'event'::character varying(8) AS type,
       e.id,
       e.type AS event_type,
       e.exe_path,
       e.name,
       to_char(e.happened_at, 'MM-DD HH24:MI:SS')::character varying(14) AS at_text,
       e.record_id,
       e.code,
       e.module,
       e.seconds,
       e.priority_from,
       e.priority_to,
       e.other_path,
       e.up_kbps,
       e.cpu_pct,
       e.mem_mb,
       e.block,
       e.protocol,
       e.address,
       e.source,
       e.location,
       NULL::integer AS crash_count,
       NULL::character varying(8)[] AS columns,
       row_number() OVER (ORDER BY e.happened_at DESC, e.id DESC) AS sort
  FROM dbo.monitor AS e
 WHERE e.type NOT IN ('seen', 'startup', 'columns')
UNION ALL
SELECT 'limit', l.id, NULL, l.exe_path, l.name, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       l.up_kbps, l.cpu_pct, l.mem_mb, l.block, NULL, NULL, NULL, NULL, NULL, NULL, 0
  FROM latest AS l
UNION ALL
SELECT 'crashes', NULL, NULL, min(c.exe_path), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, count(*)::integer, NULL, 0
  FROM dbo.monitor AS c
 WHERE c.type = 'crash'
 GROUP BY lower(c.exe_path)
UNION ALL
SELECT s.type, s.id, NULL, s.exe_path, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       NULL, NULL, NULL, NULL, NULL, NULL, s.source, s.location, NULL, NULL, 0
  FROM dbo.monitor AS s
 WHERE s.type IN ('seen', 'startup')
UNION ALL
SELECT 'columns', c.id, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, c.columns, 0
  FROM dbo.monitor AS c
 WHERE c.type = 'columns'
UNION ALL
SELECT 'since', NULL, NULL, NULL, NULL, to_char(min(f.created_at), 'MM-DD HH24:MI')::character varying(14), NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0
  FROM dbo.monitor AS f
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_monitor IS '程式監控：event 一件事一列，新的在前；limit 一支程式現在的限制；crashes 一支程式閃退幾次；seen、startup 見過的程式與開機自動執行；columns 排成欄的量測項目；since 最早一筆的時刻（還沒有任何一筆是空的）';
COMMENT ON COLUMN dbo.vw_monitor.type IS 'event＝一件事；limit＝一支程式現在的限制；crashes＝一支程式閃退幾次；seen＝見過的程式；startup＝見過的開機自動執行；columns＝排成欄的量測項目；since＝從什麼時候開始記錄';
COMMENT ON COLUMN dbo.vw_monitor.id IS '[event][limit][seen][startup][columns] monitor 那一列的流水號（寫回去照它換整列）';
COMMENT ON COLUMN dbo.vw_monitor.event_type IS '[event] 哪一種事（見 monitor.type）；畫面上的事件名照它寫在 XAML';
COMMENT ON COLUMN dbo.vw_monitor.exe_path IS '[event][limit][crashes][seen][startup] 程式的路徑';
COMMENT ON COLUMN dbo.vw_monitor.name IS '[event][limit] 程式名稱';
COMMENT ON COLUMN dbo.vw_monitor.at_text IS '[event] 發生的時刻 MM-DD HH24:MI:SS；[since] 最早一筆 MM-DD HH24:MI';
COMMENT ON COLUMN dbo.vw_monitor.record_id IS '[event] Windows 事件記錄的編號（從那裡補進來的閃退、無回應）';
COMMENT ON COLUMN dbo.vw_monitor.code IS '[event] 閃退的例外碼';
COMMENT ON COLUMN dbo.vw_monitor.module IS '[event] 閃退時出錯的模組';
COMMENT ON COLUMN dbo.vw_monitor.seconds IS '[event] 沒有回應幾秒';
COMMENT ON COLUMN dbo.vw_monitor.priority_from IS '[event] 改之前的優先順序類別值';
COMMENT ON COLUMN dbo.vw_monitor.priority_to IS '[event] 改之後的優先順序類別值';
COMMENT ON COLUMN dbo.vw_monitor.other_path IS '[event] 緩和時被暫停、被繼續的那一支';
COMMENT ON COLUMN dbo.vw_monitor.up_kbps IS '[event][limit] 上傳上限 KB/s；空的是不限';
COMMENT ON COLUMN dbo.vw_monitor.cpu_pct IS '[event][limit] CPU 上限 %；空的是不限';
COMMENT ON COLUMN dbo.vw_monitor.mem_mb IS '[event][limit] 記憶體上限 MB；空的是不限';
COMMENT ON COLUMN dbo.vw_monitor.block IS '[event][limit] 不准連網路';
COMMENT ON COLUMN dbo.vw_monitor.protocol IS '[event] 開始聽的埠的協定';
COMMENT ON COLUMN dbo.vw_monitor.address IS '[event] 開始聽的位址與埠';
COMMENT ON COLUMN dbo.vw_monitor.source IS '[event][startup] 從哪裡自動執行（見 monitor.source）';
COMMENT ON COLUMN dbo.vw_monitor.location IS '[event][startup] 在那裡的哪一項';
COMMENT ON COLUMN dbo.vw_monitor.crash_count IS '[crashes] 閃退幾次';
COMMENT ON COLUMN dbo.vw_monitor.columns IS '[columns] 排成欄的量測項目代碼';
COMMENT ON COLUMN dbo.vw_monitor.sort IS '[event] 新的在前';

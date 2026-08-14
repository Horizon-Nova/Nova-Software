-- 即時取樣：度量從哪裡取、多久取一次、CPU 怎麼換算。
-- 採集器對這些表逐列執行，不認得任何特定度量、特定軟體，也不認得「機器」與「軟體」的差別。

-- 每個度量由哪一種方式取得。參數放在該方式自己的表，取得方式不同、需要的參數本來就不同。
-- divisor 是原始值換成顯示單位要除的數：位元組→MB、位元組/秒→Mbps、100 奈秒→百分比。
CREATE TABLE metric_source (
    metric_key TEXT PRIMARY KEY,
    provider   TEXT NOT NULL,
    divisor    REAL NOT NULL
);

INSERT INTO metric_source VALUES
    ('cpu_pct',          'perfcounter', 100000),
    ('ram_mb',           'perfcounter', 1048576),
    ('disk_mbs',         'perfcounter', 1048576),
    ('net_mbps',         'etw',         131072),
    ('machine_cpu_pct',  'perfcounter', 100000),
    ('machine_ram_mb',   'perfcounter', 1048576),
    ('machine_net_mbps', 'perfcounter', 131072);

-- perfcounter 方式的參數。
-- kind 是原始值的形狀：instant 直接用，rate 取兩輪之差除以間隔秒數，rate_inverse 為 100 減去該速率。
-- Processor Information 的原始值計的是閒置時間，故為 rate_inverse。
-- match_kind 是 instance 的比對方式：indexed 取該名稱與該名稱#n，exact 只取同名，all 取全部 instance。
CREATE TABLE perfcounter_source (
    metric_key TEXT PRIMARY KEY REFERENCES metric_source(metric_key),
    category   TEXT NOT NULL,
    counter    TEXT NOT NULL,
    kind       TEXT NOT NULL,
    match_kind      TEXT NOT NULL
);

INSERT INTO perfcounter_source VALUES
    ('cpu_pct',          'Process',               '% Processor Time',  'rate',         'indexed'),
    ('ram_mb',           'Process',               'Working Set',       'instant',      'indexed'),
    ('disk_mbs',         'Process',               'IO Data Bytes/sec', 'rate',         'indexed'),
    ('machine_cpu_pct',  'Processor Information', '% Processor Time',  'rate_inverse', 'exact'),
    ('machine_ram_mb',   'Memory',                'Committed Bytes',   'instant',      'exact'),
    ('machine_net_mbps', 'Network Interface',     'Bytes Total/sec',   'rate',         'all');

-- etw 方式的參數。
CREATE TABLE etw_source (
    metric_key    TEXT PRIMARY KEY REFERENCES metric_source(metric_key),
    provider_name TEXT NOT NULL,
    provider_guid TEXT NOT NULL
);

INSERT INTO etw_source VALUES
    ('net_mbps', 'Microsoft-Windows-Kernel-Network', '{7DD42A49-5329-4832-8DFD-43D979153A88}');

-- 機器整體的度量讀哪一個 instance。這裡有列的度量屬機器，沒有的屬每個軟體。
CREATE TABLE machine_metric (
    metric_key TEXT PRIMARY KEY REFERENCES metric_source(metric_key),
    instance   TEXT NOT NULL
);

INSERT INTO machine_metric VALUES
    ('machine_cpu_pct',  '_Total'),
    ('machine_ram_mb',   'systemdiagnosticsperfcounterlibsingleinstance'),
    ('machine_net_mbps', '');

-- 每種取得方式目前可不可用。採集器啟動時更新；不可用者不會出現在工作清單，
-- 該度量沒有讀數，畫面顯示 N/A。取代「沒權限就跳過」這種判斷。
CREATE TABLE provider_status (
    provider  TEXT PRIMARY KEY,
    available INTEGER NOT NULL,
    reason    TEXT NOT NULL
);

INSERT INTO provider_status VALUES
    ('perfcounter', 0, '尚未啟動'),
    ('etw',         0, '尚未啟動');

-- 本機事實。行程的 CPU 計數器給的是相對單核的百分比，除以此數才是工作管理員顯示的值。
CREATE TABLE machine_info (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

INSERT INTO machine_info VALUES ('logical_processors', '1');

-- 取樣節奏。
CREATE TABLE sampling (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

INSERT INTO sampling VALUES
    ('interval_seconds', '2'),
    ('sampled_at', '—');

-- 採集器要的工作清單。機器與每個執行中的軟體是同一種列，差別只在 scope_id 與 instance。
-- 取得方式不可用時該度量不會出現在這裡，也就沒有讀數。
CREATE VIEW v_sampling_target AS
SELECT s.id AS scope_id,
       ms.metric_key,
       ms.provider,
       ms.divisor,
       s.id AS instance,
       CAST(IFNULL(pc.category, '') AS TEXT) AS category,
       CAST(IFNULL(pc.counter,  '') AS TEXT) AS counter,
       CAST(IFNULL(pc.kind,     '') AS TEXT) AS kind,
       CAST(IFNULL(pc.match_kind, '') AS TEXT) AS match_kind,
       CAST(IFNULL(et.provider_name, '') AS TEXT) AS etw_provider,
       CAST(IFNULL(et.provider_guid, '') AS TEXT) AS etw_guid
FROM software s
CROSS JOIN metric_source ms
JOIN provider_status ps ON ps.provider = ms.provider AND ps.available = 1
LEFT JOIN perfcounter_source pc ON pc.metric_key = ms.metric_key
LEFT JOIN etw_source        et  ON et.metric_key = ms.metric_key
WHERE s.state = 'running'
  AND ms.metric_key NOT IN (SELECT metric_key FROM machine_metric)

UNION ALL

SELECT CAST('machine' AS TEXT) AS scope_id,
       ms.metric_key,
       ms.provider,
       ms.divisor,
       mm.instance,
       CAST(IFNULL(pc.category, '') AS TEXT),
       CAST(IFNULL(pc.counter,  '') AS TEXT),
       CAST(IFNULL(pc.kind,     '') AS TEXT),
       CAST(IFNULL(pc.match_kind, '') AS TEXT),
       CAST(IFNULL(et.provider_name, '') AS TEXT),
       CAST(IFNULL(et.provider_guid, '') AS TEXT)
FROM machine_metric mm
JOIN metric_source ms   ON ms.metric_key = mm.metric_key
JOIN provider_status ps ON ps.provider = ms.provider AND ps.available = 1
LEFT JOIN perfcounter_source pc ON pc.metric_key = ms.metric_key
LEFT JOIN etw_source        et  ON et.metric_key = ms.metric_key;

-- 系統助理：對照表、固定欄位表、探索出來的軟體，以及畫面所需的全部投影。
-- 專案端只查 v_ 開頭的投影，不做任何值域對映、門檻分級、過濾或排序決策。

-- ── 對照表 ─────────────────────────────────────────────

-- 執行狀態的呈現。狀態是探索觀測到的結果，沒有寫入路徑，故不含「下一個狀態」。
CREATE TABLE state_presentation (
    state         TEXT PRIMARY KEY,
    rank          INTEGER NOT NULL,
    label         TEXT    NOT NULL,
    log_level     TEXT    NOT NULL,
    shows_reading INTEGER NOT NULL
);

INSERT INTO state_presentation VALUES
    ('running', 2, '執行中', 'warning', 1),
    ('stopped', 0, '已停止', 'info',    0),
    ('error',   1, '錯誤',   'info',    0);

-- 操作鈕。key 為（狀態、是否為系統關鍵行程）。
-- action 的值域即專案端認得的動作；enabled 為 0 者不回傳動作，該次按下沒有東西可執行。
CREATE TABLE toggle_action (
    state       TEXT    NOT NULL REFERENCES state_presentation(state),
    is_critical INTEGER NOT NULL,
    action      TEXT    NOT NULL,
    label       TEXT    NOT NULL,
    title       TEXT    NOT NULL,
    icon        TEXT    NOT NULL,
    enabled     INTEGER NOT NULL,
    PRIMARY KEY (state, is_critical)
);

INSERT INTO toggle_action VALUES
    ('running', 0, 'terminate', '停用', '停用（不論是否當掉，強制結束）',     'square', 1),
    ('running', 1, '',          '停用', '系統關鍵行程，結束會使 Windows 立即停止', 'square', 0),
    ('stopped', 0, 'launch',    '啟動', '啟動',                               'play',   1),
    ('stopped', 1, '',          '啟動', '系統關鍵行程，由 Windows 自行管理',   'play',   0),
    ('error',   0, 'launch',    '啟動', '啟動',                               'play',   1),
    ('error',   1, '',          '啟動', '系統關鍵行程，由 Windows 自行管理',   'play',   0);

-- 佔用熱度：每個度量一組門檻，取最後一個不大於當前值者。
CREATE TABLE heat_band (
    metric    TEXT NOT NULL,
    threshold REAL NOT NULL,
    level     TEXT NOT NULL,
    PRIMARY KEY (metric, threshold)
);

INSERT INTO heat_band VALUES
    ('cpu',   0, 'normal'), ('cpu',  15, 'warn'), ('cpu',  22, 'hot'),
    ('net',   0, 'normal'), ('net', 100, 'warn'), ('net', 300, 'hot');

-- 排序方向。
CREATE TABLE sort_direction (
    name     TEXT PRIMARY KEY,
    sign     INTEGER NOT NULL,
    opposite TEXT    NOT NULL,
    arrow    TEXT    NOT NULL
);

INSERT INTO sort_direction VALUES
    ('asc',   1, 'desc', 'chevron-up'),
    ('desc', -1, 'asc',  'chevron-down');

-- 可排序欄位。position 為畫面上由左到右的順序，width 為 NULL 者填滿剩餘寬度。
CREATE TABLE sort_column (
    key               TEXT PRIMARY KEY,
    label             TEXT    NOT NULL,
    alignment         TEXT    NOT NULL,
    width             REAL,
    default_direction TEXT    NOT NULL REFERENCES sort_direction(name),
    position          INTEGER NOT NULL
);

INSERT INTO sort_column VALUES
    ('name',    '軟體',     'Left',  NULL, 'asc',  0),
    ('state',   '狀態',     'Right',   88, 'asc',  1),
    ('cpuPct',  'CPU',      'Right',   96, 'desc', 2),
    ('ramMB',   '記憶體',   'Right',  104, 'desc', 3),
    ('netMbps', '網路',     'Right',   96, 'desc', 4),
    ('diskMBs', '磁碟 I/O', 'Right',   88, 'desc', 5);

-- 清單要顯示哪些執行狀態。切換鈕在這份清單裡循環。
CREATE TABLE state_filter (
    key         TEXT PRIMARY KEY,
    label       TEXT    NOT NULL,
    next_key    TEXT    NOT NULL,
    highlighted INTEGER NOT NULL
);

INSERT INTO state_filter VALUES
    ('all',     '只顯示執行中', 'running', 0),
    ('running', '只顯示執行中', 'all',     1);

CREATE TABLE state_filter_state (
    filter_key TEXT NOT NULL REFERENCES state_filter(key),
    state      TEXT NOT NULL REFERENCES state_presentation(state),
    PRIMARY KEY (filter_key, state)
);

INSERT INTO state_filter_state VALUES
    ('all', 'running'), ('all', 'stopped'), ('all', 'error'),
    ('running', 'running');

-- 欄位的可動狀態：可否編輯、草稿是否與原值不同、原值是否為 NULL，三者的每一種組合都有一列。
CREATE TABLE edit_state (
    editable   INTEGER NOT NULL,
    dirty      INTEGER NOT NULL,
    present    INTEGER NOT NULL,
    state      TEXT    NOT NULL,
    icon_title TEXT    NOT NULL,
    placeholder TEXT   NOT NULL,
    PRIMARY KEY (editable, dirty, present)
);

INSERT INTO edit_state VALUES
    (0, 0, 0, 'locked',     '唯讀，由系統回報',   'N/A'),
    (0, 0, 1, 'locked',     '唯讀，由系統回報',   ''),
    (0, 1, 0, 'locked',     '唯讀，由系統回報',   'N/A'),
    (0, 1, 1, 'locked',     '唯讀，由系統回報',   ''),
    (1, 0, 0, 'adjustable', '尚未設定，可直接填入', 'N/A'),
    (1, 0, 1, 'adjustable', '可調整',             ''),
    (1, 1, 0, 'modified',   '已修改，尚未套用',   'N/A'),
    (1, 1, 1, 'modified',   '已修改，尚未套用',   '');

-- 版面字串。帶 %s 的位置由取得的值填入，順序即 SQL 註解所列。
CREATE TABLE text_template (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- summary：軟體數、CPU、記憶體、網路、取樣時刻
INSERT INTO text_template VALUES
    ('summary', '{0} 個軟體 · CPU {1} · 記憶體 {2} · 網路 {3} · 取樣 {4}');

-- ── 主表 ───────────────────────────────────────────────

-- 固定欄位表：每個軟體都跑同一份。
CREATE TABLE field_definition (
    key      TEXT PRIMARY KEY,
    label    TEXT    NOT NULL,
    unit     TEXT    NOT NULL,
    editable INTEGER NOT NULL,
    note     TEXT    NOT NULL,
    min      INTEGER,
    max      INTEGER,
    position INTEGER NOT NULL
);

INSERT INTO field_definition VALUES
    ('netCap',      '網路頻寬上限', 'Mbps', 1, '上傳／下載節流，超過即排隊',   1,     1000,  0),
    ('ramCap',      '記憶體上限',   'MB',   1, '超過即回收快取或重啟行程',     128,   16384, 1),
    ('cpuCap',      'CPU 使用上限', '%',    1, '背景作業的節流上限',           5,     100,   2),
    ('parallel',    '同時作業數',   '個',   1, '越多越吃頻寬與 I/O',           1,     64,    3),
    ('interval',    '排程間隔',     '分',   1, '兩次自動作業之間的間隔',       1,     1440,  4),
    ('windowStart', '作業時段起點', '時',   1, '整點制，避開上班時段',         0,     23,    5),
    ('retainDays',  '資料保留天數', '天',   1, '逾期自動清除',                 1,     365,   6),
    ('autoStart',   '開機自動啟動', '',     1, '1 = 啟用',                     0,     1,     7),
    ('version',     '版本',         '',     0, '由安裝程式決定',               NULL,  NULL,  8),
    ('installPath', '安裝路徑',     '',     0, '由安裝程式決定',               NULL,  NULL,  9),
    ('lastRun',     '上次作業完成', '',     0, '系統回報',                     NULL,  NULL,  10);

-- 這台機器上的軟體。內容全部來自探索，沒有預先寫好的名單。
-- id 即行程檔名：同名的多個行程聚合成同一列，與工作管理員的分組一致。
-- 探索取不到的欄位為 NULL（權限不足時讀不到路徑與版本），不以空字串代替。
CREATE TABLE software (
    id           TEXT PRIMARY KEY,
    product_name TEXT,
    company_name TEXT,
    file_version TEXT,
    exe_path     TEXT,
    has_window   INTEGER NOT NULL,
    session_id   INTEGER NOT NULL,
    is_critical  INTEGER NOT NULL,
    state        TEXT    NOT NULL REFERENCES state_presentation(state)
);

-- 一輪觀測的原始結果。採集器每輪清空重寫，再以集合運算併入 software。
CREATE TABLE process_seen (
    id           TEXT PRIMARY KEY,
    product_name TEXT,
    company_name TEXT,
    file_version TEXT,
    exe_path     TEXT,
    has_window   INTEGER NOT NULL,
    session_id   INTEGER NOT NULL,
    is_critical  INTEGER NOT NULL
);

-- 全部讀數。scope_id 為軟體 id 或 'machine'，兩者走同一條採集路徑。
-- 取不到的度量不在表裡，畫面顯示 N/A。
CREATE TABLE reading (
    scope_id   TEXT NOT NULL,
    metric_key TEXT NOT NULL,
    value      REAL NOT NULL,
    PRIMARY KEY (scope_id, metric_key)
);

-- 使用者設定的配額值。沒有設定的欄位 value 為 NULL，畫面顯示 N/A。
CREATE TABLE software_value (
    software_id TEXT NOT NULL REFERENCES software(id),
    field_key   TEXT NOT NULL REFERENCES field_definition(key),
    value       TEXT,
    PRIMARY KEY (software_id, field_key)
);

-- 使用者尚未套用的配額草稿。套用或還原時刪除。
CREATE TABLE quota_draft (
    software_id TEXT NOT NULL REFERENCES software(id),
    field_key   TEXT NOT NULL REFERENCES field_definition(key),
    value       TEXT,
    PRIMARY KEY (software_id, field_key)
);

-- ── 投影 ───────────────────────────────────────────────

-- 顯示名稱與發行者：候選排成一張表，取第一個非 NULL 者。新增一種來源＝多一段 UNION。
CREATE VIEW v_software_label AS
SELECT s.id,
       (SELECT value FROM (
            SELECT 0 AS rank, s.product_name AS value
            UNION ALL SELECT 1, s.id)
         WHERE value IS NOT NULL ORDER BY rank LIMIT 1) AS name,
       (SELECT value FROM (
            SELECT 0 AS rank, s.company_name AS value
            UNION ALL SELECT 1, '')
         WHERE value IS NOT NULL ORDER BY rank LIMIT 1) AS publisher
FROM software s;

-- 系統回報的唯讀欄位由 software 的欄位直接供給。新增一個系統欄位＝多一段 UNION。
CREATE VIEW v_software_system_value AS
SELECT id AS software_id, CAST('version' AS TEXT) AS field_key, file_version AS value FROM software
UNION ALL
SELECT id,                'installPath',                exe_path            FROM software;

-- 按下操作鈕要執行的動作。不可按者與沒有安裝路徑者查不到列，該次按下沒有東西可執行。
CREATE VIEW v_toggle_action AS
SELECT s.id AS software_id,
       t.action,
       l.name,
       p.log_level,
       t.label,
       s.exe_path
FROM software s
JOIN v_software_label l    ON l.id = s.id
JOIN state_presentation p  ON p.state = s.state
JOIN toggle_action t       ON t.state = s.state AND t.is_critical = s.is_critical
WHERE t.enabled = 1;

-- 清單的一列：已經是畫面要顯示的字樣與等級，專案端不再加工。
-- CPU 依核心數換算後才比對熱度門檻。停止中的軟體不顯示讀數，取不到的度量顯示 N/A。
CREATE VIEW v_software_row AS
SELECT
    s.id,
    l.name,
    l.publisher,
    s.state,
    p.label        AS state_label,
    t.label        AS toggle_label,
    t.title        AS toggle_title,
    t.icon         AS toggle_icon,
    CAST(t.enabled AS INTEGER)                                        AS toggle_enabled,
    p.log_level,
    CAST(l.publisher
        || IFNULL(' · v' || s.file_version, '')
        || ' · ' || p.label AS TEXT) AS meta_text,
    CAST(CASE WHEN p.shows_reading = 0 THEN '—'
         WHEN rc.value IS NULL    THEN 'N/A'
         ELSE printf('%.1f%%', rc.value / m.cores) END AS TEXT) AS cpu_text,
    CAST(CASE WHEN p.shows_reading = 0 THEN '—'
         WHEN rr.value IS NULL    THEN 'N/A'
         ELSE printf('%.2f GB', rr.value / 1024.0) END AS TEXT) AS ram_text,
    CAST(CASE WHEN p.shows_reading = 0 THEN '—'
         WHEN rn.value IS NULL    THEN 'N/A'
         ELSE CAST(CAST(rn.value AS INTEGER) AS TEXT) || ' Mbps' END AS TEXT) AS net_text,
    CAST(CASE WHEN p.shows_reading = 0 THEN '—'
         WHEN rd.value IS NULL    THEN 'N/A'
         ELSE printf('%.1f MB/s', rd.value) END AS TEXT) AS disk_text,
    CAST(IFNULL((SELECT b.level FROM heat_band b
             WHERE b.metric = 'cpu' AND b.threshold <= rc.value / m.cores
             ORDER BY b.threshold DESC LIMIT 1), 'normal') AS TEXT)       AS cpu_level,
    CAST(IFNULL((SELECT b.level FROM heat_band b
             WHERE b.metric = 'net' AND b.threshold <= rn.value
             ORDER BY b.threshold DESC LIMIT 1), 'normal') AS TEXT)       AS net_level,
    CAST(IFNULL(rc.value / m.cores, 0.0) AS REAL)                     AS cpu_pct,
    CAST(IFNULL(rr.value, 0.0) AS REAL)                               AS ram_mb,
    CAST(IFNULL(rn.value, 0.0) AS REAL)                               AS net_mbps,
    CAST(IFNULL(rd.value, 0.0) AS REAL)                               AS disk_mbs,
    CAST((SELECT COUNT(*) FROM quota_draft d
      JOIN field_definition f ON f.key = d.field_key
      LEFT JOIN software_value v ON v.software_id = d.software_id AND v.field_key = d.field_key
      WHERE d.software_id = s.id AND f.editable = 1
        AND IFNULL(d.value, '') <> IFNULL(v.value, '')) AS INTEGER) AS changed_count
FROM software s
JOIN v_software_label l   ON l.id = s.id
JOIN state_presentation p ON p.state = s.state
JOIN toggle_action t      ON t.state = s.state AND t.is_critical = s.is_critical
LEFT JOIN reading rc ON rc.scope_id = s.id AND rc.metric_key = 'cpu_pct'
LEFT JOIN reading rr ON rr.scope_id = s.id AND rr.metric_key = 'ram_mb'
LEFT JOIN reading rn ON rn.scope_id = s.id AND rn.metric_key = 'net_mbps'
LEFT JOIN reading rd ON rd.scope_id = s.id AND rd.metric_key = 'disk_mbs'
CROSS JOIN (SELECT CAST(value AS REAL) AS cores FROM machine_info WHERE key = 'logical_processors') m;

-- 機器整體的用量。是 Windows 自己量的值，不是把每個軟體加起來——加總永遠等不到系統值。
CREATE VIEW v_machine_summary AS
SELECT
    IFNULL(printf('%.1f%%', c.value), 'N/A')           AS cpu_text,
    IFNULL(printf('%.2f GB', r.value / 1024.0), 'N/A') AS ram_text,
    IFNULL(printf('%.1f Mbps', n.value), 'N/A')        AS net_text
FROM (SELECT 1) one
LEFT JOIN reading c ON c.scope_id = 'machine' AND c.metric_key = 'machine_cpu_pct'
LEFT JOIN reading r ON r.scope_id = 'machine' AND r.metric_key = 'machine_ram_mb'
LEFT JOIN reading n ON n.scope_id = 'machine' AND n.metric_key = 'machine_net_mbps';

-- 每個軟體在每個可排序欄位上的次序值。文字欄位換成字典序位次，讓方向一律以正負號表達。
-- 讀數取自 v_software_row：那裡已把 NULL 給成 0，取不到值的列才會排在最後。
CREATE VIEW v_software_sort AS
SELECT software_id, CAST(column_key AS TEXT) AS column_key, CAST(sort_value AS REAL) AS sort_value
FROM (
    SELECT r.id AS software_id, 'name' AS column_key,
           ROW_NUMBER() OVER (ORDER BY r.name) AS sort_value
    FROM v_software_row r
    UNION ALL
    SELECT r.id, 'state', p.rank
    FROM v_software_row r JOIN state_presentation p ON p.state = r.state
    UNION ALL SELECT r.id, 'cpuPct',  r.cpu_pct  FROM v_software_row r
    UNION ALL SELECT r.id, 'ramMB',   r.ram_mb   FROM v_software_row r
    UNION ALL SELECT r.id, 'netMbps', r.net_mbps FROM v_software_row r
    UNION ALL SELECT r.id, 'diskMBs', r.disk_mbs FROM v_software_row r
);

-- 配額工作面的一列：固定欄位表 × 某個軟體，缺值的欄位仍然在，只是 value 為 NULL。
-- 唯讀欄位的值來自系統投影，可編輯欄位的值來自 software_value，兩者不重疊。
CREATE VIEW v_software_field AS
SELECT
    s.id AS software_id,
    f.key AS field_key,
    f.label,
    f.unit,
    f.editable,
    f.position,
    CAST(IFNULL(sv.value, v.value) AS TEXT) AS stored_value,
    d.value                   AS draft_value,
    CAST(IFNULL(d.value, IFNULL(sv.value, IFNULL(v.value, ''))) AS TEXT) AS display_value,
    e.state        AS edit_state,
    e.icon_title,
    e.placeholder,
    CAST(TRIM(f.note || CASE WHEN f.min IS NULL THEN '' ELSE ' · 可設 ' || f.min || '–' || f.max END) AS TEXT) AS note
FROM software s
CROSS JOIN field_definition f
LEFT JOIN software_value v           ON v.software_id  = s.id AND v.field_key  = f.key
LEFT JOIN v_software_system_value sv ON sv.software_id = s.id AND sv.field_key = f.key
LEFT JOIN quota_draft d              ON d.software_id  = s.id AND d.field_key  = f.key
JOIN edit_state e
      ON e.editable = f.editable
     AND e.dirty    = (CASE WHEN d.software_id IS NOT NULL
                             AND IFNULL(d.value, '') <> IFNULL(v.value, '') THEN 1 ELSE 0 END)
     AND e.present  = (CASE WHEN IFNULL(sv.value, v.value) IS NULL THEN 0 ELSE 1 END);

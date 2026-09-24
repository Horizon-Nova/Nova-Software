
-- 設定：一列一項，種類寫在 kind，選項放 jsonb
CREATE TABLE IF NOT EXISTS dbo.setting_management (
    id             serial PRIMARY KEY,
    type           text    NOT NULL DEFAULT 'setting',
    area_key       text    NOT NULL,
    area_label     text    NOT NULL,
    section_key    text    NOT NULL,
    section_label  text    NOT NULL,
    section_note   text,
    group_label    text    NOT NULL,
    setting_key    text,
    kind           text    NOT NULL,
    label          text    NOT NULL,
    inline         text,
    note           text,
    value          text,
    fallback       text,
    options        jsonb   NOT NULL DEFAULT '[]',
    danger         boolean NOT NULL DEFAULT false,
    button         text,
    sort           integer NOT NULL
);

-- 問答：一列一則；type 區分是誰說的那幾則與快速提問
CREATE TABLE IF NOT EXISTS dbo.message_management (
    id         serial PRIMARY KEY,
    type       text NOT NULL DEFAULT 'message',
    who        text,
    text       text NOT NULL,
    cite       text,
    created_at timestamptz NOT NULL DEFAULT now()
);

-- 設定這個讀取者一支：排序由 view 做完
CREATE OR REPLACE VIEW dbo.vw_setting AS
SELECT s.id, s.type, s.area_key, s.area_label, s.section_key, s.section_label, s.section_note,
       s.group_label, s.setting_key, s.kind, s.label, s.inline, s.note, s.value, s.fallback,
       s.options, s.danger, s.button, s.sort
  FROM dbo.setting_management AS s
 ORDER BY s.sort;

-- AI 助理這個讀取者一支：問答依時間、快速提問依編號
CREATE OR REPLACE VIEW dbo.vw_assist AS
SELECT m.id, m.type, m.who, m.text, m.cite, m.created_at
  FROM dbo.message_management AS m
 ORDER BY m.created_at, m.id;

-- 設定的每一項
INSERT INTO dbo.setting_management (area_key, area_label, section_key, section_label, section_note, group_label, setting_key, kind, label, inline, note, value, fallback, options, danger, sort) VALUES
  ('top', '常用', 'common', '常用設定', '現場最常調的幾項，都可以在各分類裡找到完整設定。', '常用設定', 'bigTargets', 'toggle', '大按鈕模式', '按鈕與輸入框加高，戴手套好按', '針對現場戴手套操作放大命中範圍。', 'true', 'true', '[]'::jsonb, false, 1),
  ('top', '常用', 'common', '常用設定', '現場最常調的幾項，都可以在各分類裡找到完整設定。', '常用設定', 'textScale', 'choice', '文字大小', NULL, '站在機台旁看螢幕時可以放大。', '標準 100%', '標準 100%', '["標準 100%","放大 115%","放大 130%"]'::jsonb, false, 2),
  ('top', '常用', 'common', '常用設定', '現場最常調的幾項，都可以在各分類裡找到完整設定。', '常用設定', 'updateWindow', 'choice', '允許更新的時段', NULL, '避開產線作業時間，更新不會中斷正在跑的工具。', '換班時段（07:50 / 15:50 / 23:50）', '換班時段（07:50 / 15:50 / 23:50）', '["換班時段（07:50 / 15:50 / 23:50）","每天 02:00 – 04:00","只在我按下更新時"]'::jsonb, false, 3),
  ('top', '常用', 'common', '常用設定', '現場最常調的幾項，都可以在各分類裡找到完整設定。', '常用設定', 'aiDock', 'toggle', 'AI 助理停靠在右側面板', '問 AI 時不關掉正在做的作業', '關閉會改成整頁開啟，作業畫面會被頂掉。', 'true', 'true', '[]'::jsonb, false, 4),
  ('device', '這台裝置', 'general', '一般', '這台裝置的啟動與登入行為，變更在下次開機生效。', '啟動', 'startView', 'choice', '開啟後顯示', NULL, '現場多半固定用幾支工具，可以跳過目錄。', '工具目錄', '工具目錄', '["工具目錄","上次用的工具","IT 必裝分類"]'::jsonb, false, 5),
  ('device', '這台裝置', 'general', '一般', '這台裝置的啟動與登入行為，變更在下次開機生效。', '啟動', 'launchOnBoot', 'toggle', '開機時自動啟動', '交接班不必手動開啟', NULL, 'true', 'true', '[]'::jsonb, false, 6),
  ('device', '這台裝置', 'general', '一般', '這台裝置的啟動與登入行為，變更在下次開機生效。', '啟動', 'keepSignedIn', 'toggle', '保持登入狀態', '共用電腦建議關閉', '關閉後下班時間會自動登出。', 'true', 'true', '[]'::jsonb, false, 7),
  ('device', '這台裝置', 'update', '更新與安裝', '必裝工具的更新由資訊部推送，無法關閉。', '時機', 'updateAuto', 'toggle', '自動下載並安裝更新', '關閉後仍會提醒，但要自己按安裝', NULL, 'true', 'true', '[]'::jsonb, false, 8),
  ('device', '這台裝置', 'update', '更新與安裝', '必裝工具的更新由資訊部推送，無法關閉。', '時機', 'updateWindow', 'choice', '允許更新的時段', NULL, '避開產線作業時間，更新不會中斷正在跑的工具。', '換班時段（07:50 / 15:50 / 23:50）', '換班時段（07:50 / 15:50 / 23:50）', '["換班時段（07:50 / 15:50 / 23:50）","每天 02:00 – 04:00","只在我按下更新時"]'::jsonb, false, 9),
  ('device', '這台裝置', 'update', '更新與安裝', '必裝工具的更新由資訊部推送，無法關閉。', '來源與權限', 'source', 'choice', '套件下載來源', NULL, '廠區離線部署，不會連到外網。', '廠內套件庫（10.42.0.30）', '廠內套件庫（10.42.0.30）', '["廠內套件庫（10.42.0.30）","備援套件庫（10.42.0.31）"]'::jsonb, false, 10),
  ('device', '這台裝置', 'update', '更新與安裝', '必裝工具的更新由資訊部推送，無法關閉。', '來源與權限', 'updateNeedApproval', 'toggle', '安裝非必裝工具需主管核可', '共用機台建議開啟', NULL, 'false', 'false', '[]'::jsonb, false, 11),
  ('device', '這台裝置', 'update', '更新與安裝', '必裝工具的更新由資訊部推送，無法關閉。', '來源與權限', NULL, 'action', '立即檢查更新', NULL, '上次同步：09:12 匯入', NULL, NULL, '[]'::jsonb, false, 12),
  ('device', '這台裝置', 'notify', '通知', '必讀公告與必裝提醒由公司政策決定。', '提醒', NULL, 'info', '必讀公告與必裝提醒', NULL, NULL, '由公司政策開啟，無法關閉', '由公司政策開啟，無法關閉', '[]'::jsonb, false, 13),
  ('device', '這台裝置', 'notify', '通知', '必讀公告與必裝提醒由公司政策決定。', '提醒', 'notifyTicket', 'toggle', '工單與遠端支援狀態', '服務台回覆或連線結束時通知', NULL, 'true', 'true', '[]'::jsonb, false, 14),
  ('device', '這台裝置', 'notify', '通知', '必讀公告與必裝提醒由公司政策決定。', '提醒', 'notifySound', 'toggle', '通知音效', '廠區噪音大時通常關閉', NULL, 'false', 'false', '[]'::jsonb, false, 15),
  ('work', '工作方式', 'ai', 'AI 助理', '回答取自公司手冊、SOP 與歷史工單，模型在廠內執行，不連外網。', '顯示方式', 'aiDock', 'toggle', '停靠在右側面板', '問 AI 時不關掉正在做的作業', '關閉會改成整頁開啟。', 'true', 'true', '[]'::jsonb, false, 16),
  ('work', '工作方式', 'ai', 'AI 助理', '回答取自公司手冊、SOP 與歷史工單，模型在廠內執行，不連外網。', '顯示方式', 'aiOpenOnStart', 'toggle', '開啟軟體時就展開助理', '新人上線期間可開啟', NULL, 'false', 'false', '[]'::jsonb, false, 17),
  ('work', '工作方式', 'ai', 'AI 助理', '回答取自公司手冊、SOP 與歷史工單，模型在廠內執行，不連外網。', '回答與紀錄', 'aiCite', 'toggle', '回答附上來源文件', '可追到 SOP 版次，建議開啟', NULL, 'true', 'true', '[]'::jsonb, false, 18),
  ('work', '工作方式', 'ai', 'AI 助理', '回答取自公司手冊、SOP 與歷史工單，模型在廠內執行，不連外網。', '回答與紀錄', 'aiKeepLog', 'toggle', '保留問答紀錄 30 天', '供資訊部改善手冊', '紀錄只留在這台裝置。', 'false', 'false', '[]'::jsonb, false, 19),
  ('work', '工作方式', 'ai', 'AI 助理', '回答取自公司手冊、SOP 與歷史工單，模型在廠內執行，不連外網。', '回答與紀錄', NULL, 'action', '清除本機問答紀錄', NULL, '只清這台裝置上的紀錄。', NULL, NULL, '[]'::jsonb, true, 20),
  ('work', '工作方式', 'floor', '現場操作與顯示', '針對戴手套、站著操作、廠區光線強的環境調整。', '操作', 'bigTargets', 'toggle', '大按鈕模式', '按鈕與輸入框加高到 44px 以上', NULL, 'true', 'true', '[]'::jsonb, false, 21),
  ('work', '工作方式', 'floor', '現場操作與顯示', '針對戴手套、站著操作、廠區光線強的環境調整。', '操作', 'textScale', 'choice', '文字大小', NULL, '站在機台旁看螢幕時可以放大。', '標準 100%', '標準 100%', '["標準 100%","放大 115%","放大 130%"]'::jsonb, false, 22),
  ('work', '工作方式', 'floor', '現場操作與顯示', '針對戴手套、站著操作、廠區光線強的環境調整。', '操作', 'screenAwake', 'toggle', '使用中保持螢幕不休眠', '作業中不會自動鎖定', NULL, 'true', 'true', '[]'::jsonb, false, 23),
  ('work', '工作方式', 'floor', '現場操作與顯示', '針對戴手套、站著操作、廠區光線強的環境調整。', '顯示', 'highContrast', 'toggle', '高對比顯示', '日照強的區域建議開啟', NULL, 'false', 'false', '[]'::jsonb, false, 24),
  ('support', '支援', 'about', '關於本軟體', '報修或開工單時，請把下面這幾項一起提供給資訊部。', '版本與裝置', NULL, 'info', '用戶端版本', NULL, NULL, '2026.9.1 (build 26091.4)', '2026.9.1 (build 26091.4)', '[]'::jsonb, false, 25),
  ('support', '支援', 'about', '關於本軟體', '報修或開工單時，請把下面這幾項一起提供給資訊部。', '版本與裝置', NULL, 'info', '電腦名稱', NULL, NULL, 'TPE-PROD-1043', 'TPE-PROD-1043', '[]'::jsonb, false, 26),
  ('support', '支援', 'about', '關於本軟體', '報修或開工單時，請把下面這幾項一起提供給資訊部。', '版本與裝置', NULL, 'info', '作業系統', NULL, NULL, 'Windows 11 Enterprise 23H2', 'Windows 11 Enterprise 23H2', '[]'::jsonb, false, 27),
  ('support', '支援', 'about', '關於本軟體', '報修或開工單時，請把下面這幾項一起提供給資訊部。', '版本與裝置', NULL, 'info', '內網 IP', NULL, NULL, '10.42.18.107', '10.42.18.107', '[]'::jsonb, false, 28),
  ('support', '支援', 'about', '關於本軟體', '報修或開工單時，請把下面這幾項一起提供給資訊部。', '版本與裝置', NULL, 'info', '使用者', NULL, NULL, '林志豪（生產部 工程二課）', '林志豪（生產部 工程二課）', '[]'::jsonb, false, 29),
  ('support', '支援', 'about', '關於本軟體', '報修或開工單時，請把下面這幾項一起提供給資訊部。', '版本與裝置', NULL, 'info', '套件庫同步', NULL, NULL, '09:12 匯入', '09:12 匯入', '[]'::jsonb, false, 30),
  ('support', '支援', 'about', '關於本軟體', '報修或開工單時，請把下面這幾項一起提供給資訊部。', '支援', NULL, 'action', '匯出診斷包', NULL, '含版本、裝置資訊與最近錯誤紀錄，不含你的檔案內容。', NULL, NULL, '[]'::jsonb, false, 31),
  ('support', '支援', 'about', '關於本軟體', '報修或開工單時，請把下面這幾項一起提供給資訊部。', '支援', NULL, 'action', '回復成資訊部建議設定', NULL, '只重設這台裝置上的偏好，不影響已安裝的工具。', NULL, NULL, '[]'::jsonb, true, 33);
UPDATE dbo.setting_management SET button = '檢查更新' WHERE sort = 12;
UPDATE dbo.setting_management SET button = '清除' WHERE sort = 20;
UPDATE dbo.setting_management SET button = '匯出' WHERE sort = 31;
UPDATE dbo.setting_management SET button = '開遠端支援' WHERE sort = 32;
UPDATE dbo.setting_management SET button = '回復預設' WHERE sort = 33;

-- 快速提問
INSERT INTO dbo.message_management (type, text) VALUES
  ('prompt', '這台機台異音怎麼排查'),
  ('prompt', '料號 A-2041 的包裝規格'),
  ('prompt', '補打卡要怎麼送');


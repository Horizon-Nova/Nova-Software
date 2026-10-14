-- InSpecTool 的讀取拆成三支：設定（一列）、欄位、執行；舊的 vw_inspec 拿掉

DROP VIEW IF EXISTS dbo.vw_inspec;

-- InSpecTool 的設定：只有一列，每一項設定一欄，型別在這裡轉好
CREATE OR REPLACE VIEW dbo.vw_inspec_setting AS
SELECT max(i.value) FILTER (WHERE i.setting_key = 'searchFolder') AS search_folder,
       max(i.value) FILTER (WHERE i.setting_key = 'logFolder') AS log_folder,
       max(i.value) FILTER (WHERE i.setting_key = 'backupFolder') AS backup_folder,
       max(i.value) FILTER (WHERE i.setting_key = 'scanInterval')::integer AS scan_interval,
       ARRAY(SELECT jsonb_array_elements_text(max(i.options::text) FILTER (WHERE i.setting_key = 'scanInterval')::jsonb)) AS scan_options,
       coalesce(max(i.value) FILTER (WHERE i.setting_key = 'showResult') = 'true', false) AS show_result,
       nullif(max(i.value) FILTER (WHERE i.setting_key = 'leftWidth'), '')::double precision AS left_width
  FROM dbo.inspec_management AS i
 WHERE i.type = 'setting';

COMMENT ON VIEW dbo.vw_inspec_setting IS 'InSpecTool 的設定只有一列：搜尋資料夾、紀錄資料夾、備份路徑、條碼解析間隔（毫秒）與它的選項、跑完顯示結果、左欄寬度（px，沒拖過是 NULL）';

-- InSpecTool 的欄位：一個欄位一列，照順序排；也是條碼分段的順序
CREATE OR REPLACE VIEW dbo.vw_inspec_field AS
SELECT i.id,
       i.name,
       i.prompt,
       i.mode,
       i.mode = 'confirm' AS confirm,
       coalesce(i.editable, false) AS editable,
       i.sort
  FROM dbo.inspec_management AS i
 WHERE i.type = 'field'
 ORDER BY i.sort, i.id;

COMMENT ON VIEW dbo.vw_inspec_field IS 'InSpecTool 的欄位一個一列：名稱、InSpec 提示、填法、是不是先確認再填、主頁可改、順序';

-- InSpecTool 的執行：一次一列，新的在前；花了多久、有沒有歸檔在這裡算好
CREATE OR REPLACE VIEW dbo.vw_inspec_run AS
SELECT i.id,
       i.program,
       i.fields,
       i.prompts,
       i.started_at,
       i.finished_at,
       i.exit_code,
       i.export,
       i.archive,
       i.note,
       i.archive IS NOT NULL AS archived,
       to_char(i.started_at, 'YYYY-MM-DD HH24:MI:SS') AS started_text,
       CASE WHEN i.finished_at IS NULL THEN NULL
            ELSE to_char(EXTRACT(EPOCH FROM (i.finished_at - i.started_at)), 'FM999990.0') || ' 秒' END AS elapsed_text
  FROM dbo.inspec_management AS i
 WHERE i.type = 'run'
 ORDER BY i.started_at DESC, i.id DESC;

COMMENT ON VIEW dbo.vw_inspec_run IS 'InSpecTool 的執行一次一列：程式、欄位、提示、匯出、歸檔路徑、備註、有沒有歸檔、開始時刻文字、花了多久文字；新的在前';

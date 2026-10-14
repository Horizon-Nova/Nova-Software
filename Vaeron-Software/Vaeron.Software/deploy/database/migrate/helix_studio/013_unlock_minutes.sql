-- 設定頁的工具設定鎖：vw_tool_setting 帶出輸入工具金鑰後解開幾分鐘

-- 設定頁「工具」底下：有設定畫面的工具一支一列，照匯入先後排；解鎖多久從 toolKeyMinutes 那一項的值取出分鐘數
CREATE OR REPLACE VIEW dbo.vw_tool_setting AS
SELECT t.tool_no,
       t.name,
       t.settings,
       t.id AS sort,
       (SELECT nullif(regexp_replace(s.value, '\D', '', 'g'), '')::integer
          FROM dbo.setting_management AS s
         WHERE s.setting_key = 'toolKeyMinutes') AS unlock_minutes
  FROM dbo.tool_management AS t
 WHERE t.type = 'tool' AND coalesce(t.settings, '') <> ''
 ORDER BY t.id;

COMMENT ON VIEW dbo.vw_tool_setting IS '設定頁「工具」底下：有設定畫面的工具一支一列（代碼、名稱、設定畫面型別、輸入工具金鑰後設定解開幾分鐘），照匯入先後排';

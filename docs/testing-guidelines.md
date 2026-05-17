## 測試驗證記錄

### 使用方式
- **記錄格式**：`[時間] 測試項目 > 測試方法 > 步驟1 > 步驟2 > ... > 結果 > [失敗原因/修復方法]`
- **避免重複失敗**：測試前必須查看之前的測試記錄，避免重複使用已經失敗的測試方式
- **時間格式**：YYYY-MM-DD HH:MM 或 YYYY-MM-DD
### 範例：
[2024-01-01 10:00] 測試環境 > cmd > python --version > 成功
[2024-01-01 10:05] 測試環境 > cmd > pip install torch > 失敗 > 權限不足
[2024-01-01 10:10] 測試環境 > shell > pip install torch > 成功

### 測試記錄（時間軸格式）

[2026-05-11] 文件（Helix-Studio-功能描述改為「檔名-子功能」條列）> 人工對照 Net/Helix-Studio 原始碼路徑與類別職責 > [成功]（僅結構調整，無建置需求）

[2026-05-11] Helix Studio 品牌化（Vaeron/AppLogo PNG、EXE ApplicationIcon ICO、視窗 Icon、WiX ARP／捷徑圖示路徑）> PowerShell、`dotnet build` `Net/Helix-Studio/Helix-Studio.csproj` `-c Release` > [成功]（UI 視窗內側邊欄與標題列圖示需於本機啟動應用程式後以指定視窗截圖複核）

# InSpec 研究區

InSpecTool 的技術依據：怎麼在不靠畫面的情況下驅動 InSpec、知道它什麼時候跑完、讀到它的提示與結果。
研究與測試用，不是產品的一部分；產品在 `tools\InSpecTool`。原本放在 `D:\Private_project\NetToolTestArea`，2026-10-14 搬進來。

| 資料夾 | 內容 |
|---|---|
| `blueprint\` | 研究藍圖：DLL 注入、讀畫面、iscmd、無畫面啟動、AutoExport 讀值、控制項偵查等 |
| `Sandbox\RunProbe\` | 2026-10-12 實測 iscmd 的小程式；InSpecTool 現在的做法照它來 |
| `Sandbox\scripts\` | 研究期的一次性注入與探測腳本、測試用 .iwp（說明見 `Sandbox\README.md`） |
| `Sandbox\InSpecToolTest\` | InSpecTool 測試用的量測程式、log、備份資料夾 |
| `VaeronInSpecDemo\` | 第一版做法：注入 InSpec 的 C++ DLL（`InSpecHookDLL_Source`）與測試畫面（`InSpecHookDLLHost`、`InSpecAutoLaunch`） |
| `backup\` | InSpec 自己的資源檔，只在本機，不進版控 |

- 編出來的 dll、bin、obj 不進版控，要用時照原始碼重編（C++ DLL 用 `VaeronInSpecDemo\InSpecHookDLL_Source\auto_build.bat`）。
- 這裡的 C# 小專案各自獨立建置，不在 `Vaeron.Software.sln` 裡，也不觸發平台的環境準備。

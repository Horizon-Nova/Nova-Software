# 藍圖：InSpec 摘要面板完成打勾改造

> 體例：規格與決策表。每列為「結論／依據／狀態」，非時序敘述。
> 適用對象：Micro-Vu InSpec 的外掛改造（本專案 InSpecTool）。

---

## 0. 標的識別（避免命名混淆）

| 項目 | 事實 | 依據 |
|---|---|---|
| 本標的 | **Micro-Vu Corporation「InSpec for Windows」光學量測軟體** | 版本資源 CompanyName、簽章 CN=Micro-Vu Corporation of California |
| 非本標的 | Chef/Progress「Chef InSpec」（Ruby 合規稽核工具，Apache 2.0） | 僅名稱相同，無任何關聯 |
| 技術棧 | C++ / MFC100（VS2010，vc100）/ x86 32-bit | PE header（Machine 0x014C）、import mfc100u.dll |
| 安裝路徑 | `C:\Program Files (x86)\Micro-Vu Corporation\InSpec for Windows` | — |

---

## 1. 角色 × 需求對撞（設計約束來源）

| 角色 | 需求 | 對本設計的約束 |
|---|---|---|
| 現場量測員 | 量測完成時要一眼看到「通過」，不看無意義數值 | 完成時以大打勾覆蓋摘要面板 |
| 現場量測員 | 未完成時仍要看到正常量測畫面 | 覆蓋層僅在「完成」時顯示，未完成時隱藏，不干擾 |
| 開發者 | 改造要能開關、可還原 | 提供 Enable/Disable 匯出，Disable 即完全還原 |
| 維護者 | 不可破壞 InSpec 原始安裝 | 不改 InSpec 檔案，改用執行期注入覆蓋 |
| InSpec 進程穩定性 | 改造不可造成崩潰 | 覆蓋層為獨立子視窗，不子類化 InSpec 控制項 |

---

## 2. 核心技術約束（決定方案的關鍵事實）

| 約束 | 內容 | 依據 |
|---|---|---|
| **摘要內容為執行期重建，非模板控制** | 摘要面板的顯示控制項（綠勾 1264、GXWND 表格 1268、值 1266）由 InSpec 於執行期銷毀對話框模板控制項後、以自訂視窗類別（AfxWnd100u / GXWND）重建，座標與大小由程式碼決定 | 修改 InSpecRes_cht.dll 之 DIALOG #486 模板後，執行期控制項的 class/rect/style 與模板完全不符 |
| **推論** | 任何顯示改造必須於執行期進行（HookDLL 注入）；資源模板（`InSpecRes_cht.dll`）編輯對這些控制項無效 | 上列約束直接推得 |
| UI 資源 DLL | UI 資源集中於 `InSpecRes_cht.dll`（繁中，DIALOG×238 繁中 lang=0x0404）；另有 14 個語系變體 | 資源枚舉 + 語言碼 |
| 摘要對話框模板 | DIALOG #486（僅為版面佔位參考，非實際顯示來源） | 控制項 ID 集合與執行期 6/6 吻合 |

---

## 3. 執行期關鍵識別（HookDLL 定位依據）

| 目標 | 執行期識別 | 用途 |
|---|---|---|
| 主視窗 | 視窗類別 `InSpecAppFrame` | EnumWindows 進入點 |
| 摘要對話框 | `#32770`，位於 `SysTabControl32`(id 1251) 之下 | 覆蓋層的父視窗 |
| 錨定摘要對話框 | 找 class=`GXWND` 且 `GetDlgCtrlID`==**1268** 的視窗，其 `GetParent` 即摘要 `#32770` | 唯一可靠錨點（#486 才有 GXWND/1268，另一頁 #706 無） |
| **完成訊號** | `GetDlgItem(dlg, 1264)` 的 `IsWindowVisible` == true | InSpec 自身的「量測完成」判斷，直接沿用，不需逆向 |
| 被覆蓋的資訊 | GXWND(**1268**, 約 172×205)、值(**1266**) | 完成時由覆蓋層蓋住 |

---

## 4. 最終方案：HookDLL 覆蓋層

### 4.1 匯出函式（追加於 `InSpecHookDLL.def` / `.cpp`）

| 匯出 | 作用 |
|---|---|
| `EnableSummaryPatch` / `DisableSummaryPatch` | 摘要大打勾（開啟＝設旗標 `g_overlayEnabled` + 確保監看執行緒） |
| `EnableDemoButton` / `DisableDemoButton` | Demo 按鈕（開啟＝設旗標 `g_demoEnabled` + 確保監看執行緒） |
| `ThreadWrapper_*`（上列各一） | 供遠端 CreateRemoteThread 呼叫（DWORD WINAPI 簽章） |

**共用單一監看執行緒**：兩功能各有獨立開關旗標，共用同一條 `MonitorThread`；`StopMonitorIfIdle` 在兩旗標皆關時才真正結束執行緒。RVA（本次建置）：`ThreadWrapper_EnableSummaryPatch`=0x4BF0、`ThreadWrapper_EnableDemoButton`=0x4C50（每次重建須以 dumpbin 重新確認）。

### 4.2 運作規格

| 項目 | 規格 |
|---|---|
| 覆蓋層 | 註冊視窗類別 `InSpecSummaryCheckOverlay`，建為摘要 `#32770` 的子視窗（WS_CHILD \| WS_CLIPSIBLINGS），Z 序置頂 |
| 監看方式 | 監看執行緒 + `SetTimer` 輪詢，週期 **300ms** |
| 顯示邏輯 | 完成訊號 true → `ShowWindow(SW_SHOWNA)` + 置頂 + `InvalidateRect`；false → `SW_HIDE` |
| 打勾繪製 | GDI 自繪 Lucide **circle-check-big** 幾何：圓(center 12,12 r10) + 折線(9,11)-(12,14)-(22,4)，24 視框置中縮放；`ExtCreatePen` 圓端圓角、綠色 `RGB(22,163,74)`；背景白 | 
| 不侵入性 | 不子類化、不改 InSpec 控制項；覆蓋層獨立，Disable 即銷毀還原 |

### 4.3 Demo 按鈕（已實作）

| 項目 | 規格 |
|---|---|
| 位置 | 「元素」清單右側量測工具列群下方；**錨定 `ToolbarWindow32` id 1166**，取其 rect，`ScreenToClient` 到父面板（AfxWnd100u，量測工具面板）後置於工具列下方（bottom+20，148×22） |
| 視窗 | 自訂類別 `InSpecDemoButton` 子視窗；`WM_PAINT` 以 `DrawEdge(EDGE_RAISED)` 畫按鈕外觀、`DrawTextW` 置中「Demo」 |
| 行為 | `WM_LBUTTONUP` → `MessageBoxW` 標題「Demo」、內容 `Demo功能請輸入Run`（`MB_ICONINFORMATION`） |
| 定位維護 | 監看執行緒每 tick 重找錨點並 `SetWindowPos`，跟隨版面變化 |

### 4.4 實作陷阱（已內含於程式碼，勿回退）

| 陷阱 | 正解 |
|---|---|
| `SetTimer(NULL, id, …)` 會**忽略指定 id、回傳系統自配 id** | 必須用 `SetTimer` 回傳值比對 `WM_TIMER` 的 `wParam`，不可用自訂常數；`KillTimer` 亦用回傳值 |
| GDI 繪圖函式 | 建置需連結 **`gdi32.lib`**（原 auto_build.bat 未含，必須補上） |
| 原始碼含 CJK 字串字面值 | 建置須加 **`/utf-8`**（否則 cl 以 cp950 誤讀，UTF-8 的 CJK bytes 會破壞字串甚至相鄰宣告） |
| 64-bit 注入器 | InSpec 為 32-bit，注入器必須 32-bit（SysWOW64 PowerShell）；遠端模組基址須以列舉模組取得，基址無效絕不呼叫（否則存取違規拖垮 InSpec） |

---

## 5. 注入規格（測試／部署）

| 項目 | 規格 | 依據 |
|---|---|---|
| 注入器位元數 | **必須 32-bit 進程**（如 SysWOW64 PowerShell 或本專案 x86 C# 工具） | InSpec 為 32-bit；64-bit 注入器取得的 kernel32/LoadLibraryW 位址與目標不符（WOW64） |
| 注入方式 | VirtualAllocEx 寫入 DLL 路徑 → CreateRemoteThread(LoadLibraryW) | 同 DLLCore.InjectDLL |
| 取得遠端模組基址 | **列舉遠端模組**（EnumProcessModulesEx + GetModuleFileNameEx）比對 DLL 名 | LoadLibraryW 執行緒 exit code 會截斷且有 STILL_ACTIVE 競態，不可作為基址 |
| 呼叫匯出 | 遠端函式位址 = 遠端模組基址 + 匯出 RVA（dumpbin /EXPORTS 取得），再 CreateRemoteThread | 基址無效時**絕不**計算並呼叫，否則存取違規會拖垮 InSpec |

---

## 6. 建置

| 項目 | 規格 |
|---|---|
| 工具鏈 | VS x86（vcvars32），`cl /LD /MT` |
| 連結庫 | kernel32 user32 **gdi32** psapi dbghelp |
| 輸出部署 | `InSpecHookDLL.dll` → `..\Test` 與 `..\bin\Debug\Test`（比照 auto_build.bat） |
| 前置條件 | 重建前須先關閉載入該 DLL 的 InSpec，否則 LNK1104（檔案被鎖） |

---

## 7. 底層完成機制（Ghidra 反編譯 + 動態 ShowWindow hook 確認）

| 層 | 函式（RVA 相對 InSpec.exe imagebase 0x60000000） | 職責 |
|---|---|---|
| L5 量測引擎 | `FUN_602961d0` | 狀態轉換後呼叫 L4 |
| L4 完成派發 | `FUN_601a13d0` | 呼叫 L3；成功則後續動作 |
| L3 面板刷新（核心） | `FUN_601a1430` | 迭代元素容器、逐一呼叫面板更新 |
| L1 面板更新 | `FUN_6022bba0`(摘要頁) / `FUN_6021e9e0`(點頁) | 末端對指示器 `ShowWindow` |

| 事實 | 內容 | 依據 |
|---|---|---|
| control 1264 的本質 | **不是**專用完成旗標；是面板刷新函式每次執行時**無條件 `ShowWindow(SW_SHOW)`** 的副作用（L1 對物件 offset +0x94/+0x98 的 CWnd） | 反編譯 L1a line 600 / L1b line 912 |
| 面板刷新的觸發條件 | 元素容器**非空**（C++ begin≠end）且非重入（重入鎖 `local_c4[4]`） | 反編譯 L3 line 113 早退判斷 |
| 未完成不顯示的原因 | 容器為空 → L3 早退 → 面板從不刷新 → 1264 從未被 SHOW | 同上 |
| 完成語義 | **per-element（當前元素已量測）**，非整批完成 | 上述鏈完整推得 |
| 附帶 | 面板以 RTTI 區分 `Metrology::Feature` vs `Metrology::Instruction` 決定顯示數值；+0x290/+400 控制項僅 Feature 時顯示 | 反編譯 L1a line 414、602 |

**結論：** 現行「輪詢 1264 可見性」= 追蹤「當前元素已量測」，語義正確且穩定，無需改為事件驅動。

---

## 8. 交付狀態

| 需求 | 狀態 | 位置 |
|---|---|---|
| 摘要完成時顯示大打勾 | ✅ 已實作並實測通過 | HookDLL 覆蓋層（§4.2） |
| 「元素」右側工具列加 Demo 按鈕（彈窗 `Demo功能請輸入Run`） | ✅ 已實作並實測通過 | HookDLL Demo 按鈕（§4.3） |
| 完成訊號語義（per-element） | ✅ 已反編譯確認 | §7 |

無待辦項。後續若要新增功能，沿用同一監看執行緒 + 旗標模式（§4.1）。

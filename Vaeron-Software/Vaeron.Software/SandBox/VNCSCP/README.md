# VNCSCP 研究區

VNCSCP（HelixStudio 工具：管理多台跑 UltraVNC Server 的機台、測連線、批次部署檔案）的技術依據：UltraVNC 能做什麼、限制在哪、怎麼不靠它的畫面驅動它。
研究與測試用，不是產品的一部分。舊的獨立版在 `Vaeron-Software\VNCSCP\`。

| 資料夾 | 內容 |
|---|---|
| `blueprint\` | 研究藍圖，一個主題一份 |
| `scripts\` | `install-ultravnc.ps1`：把 UltraVNC 裝進 VM-Lab 的機器（Server／Client 兩種角色） |
| `vendor\` | 下載的安裝檔、測試用 VNC 密碼 `test.secret`、安裝 log；只在本機，不進版控 |

- 這裡的 C# 小專案各自獨立建置（`Directory.Build.targets` 擋住上層），不觸發平台的環境準備。

## 下載

GitHub（ultravnc/UltraVNC）沒有 Releases；安裝檔只在 uvnc.com 的下載頁（勾授權後轉到 uvnc.eu）。2026-10-14 下載。

| 版本 | 檔案 | 網址 | sha256 | 核對 |
|---|---|---|---|---|
| 1.8.3.0（最新穩定版） | `vendor\1.8.3.0\UltraVNC_1830_X64_Setup.exe` | https://uvnc.eu/download/1800/UltraVNC_1830_x64_Setup.exe | `e9c22419ef3128a707b0d004a2c58e25459528c5c12d104dea56e5e662bd131e` | 與下載頁公布值相同；簽章有效（Open Source Developer, Rudi De Vos） |
| 1.8.3.0 | `vendor\1.8.3.0\UltraVNC_1830.zip`（免安裝的執行檔） | https://uvnc.eu/download/1800/UltraVNC_1830.zip | `11163a0b9b86321bf6403eb0cf84b81f7230ce32e6291377e1fe5beb987f7e4f` | 與下載頁公布值相同 |
| 1.2.4.0（舊版比較用） | `vendor\1.2.4.0\UltraVNC_1_2_40_X64_Setup.exe` | https://www.uvnc.eu/download/1240/UltraVNC_1_2_40_X64_Setup.exe | `2636b64a08e695e3d7108f172410ec37fae9729214abb8bc0db3a0ffea75879f` | 下載頁沒公布雜湊；簽章有效（uvnc bvba） |

下載頁：1.8.3.0 https://uvnc.com/downloads/ultravnc/172-ultravnc-1-8-3-0.html ；1.2.4.0 https://uvnc.com/downloads/ultravnc/129-ultravnc-1-2-4-0.html

## 測試機（VM-Lab）

| 機器 | 位址（Default Switch，DHCP，重開可能變） | 裝了什麼 |
|---|---|---|
| `VNC-Server` | 172.18.100.169 | UltraVNC 1.8.3.0 Server＋Viewer；服務 `uvnc_service` 自動啟動、執行中；聽 `0.0.0.0:5900`（RFB）與 `0.0.0.0:5800`（HTTP）；VNC 密碼＝`vendor\test.secret` |
| `VNC-Client` | 172.18.103.28 | UltraVNC 1.8.3.0 Viewer |

- 檢查點：`clean`（剛開好的桌面）、`ultravnc-installed`（裝好、tester 在桌面）；兩台都已關機
- 回到裝好的狀態：`Restore-VMCheckpoint -VMName VNC-Server -Name ultravnc-installed; Start-VM VNC-Server`
- 兩台的 `C:\test\` 都放了上表三個檔（1.2.4.0 只放著，沒裝）
- 已量到：主機與 VNC-Client 都連得到 VNC-Server 的 5900，開頭送 `RFB 003.008`；設定檔 `ultravnc.ini` 裡的 `passwd` 解得回 `test.secret`

## 安裝方式（`scripts\install-ultravnc.ps1`）

靜默安裝參數取自原始碼 `UltraVNC_installer_x64.iss`（Inno Setup 6.6.1）：

```
UltraVNC_1830_X64_Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=… /TYPE=custom
    /COMPONENTS=UltraVNC_Server,UltraVNC_Viewer /TASKS=installservice,startservice /setpasswd=<密碼>
```

- `/setpasswd=` 交給 `setpasswd.exe`（`[Run]` 的 `{param:setpasswd|}`）；接著 `setpasswd.exe -random` 只補沒設的密碼，所以唯讀密碼是隨機的
- 1.8.3.0 的設定檔在 `C:\ProgramData\UltraVNC\ultravnc.ini`（安裝程式從程式資料夾搬過去）
- 安裝程式自己會加防火牆規則：`UltraVNC Server TCP/UDP`（入站，限 winvnc.exe）、`UltraVNC Viewer TCP/UDP`（出站）
- 要知道的事：
  - Inno 的 log 會寫出整串命令列與 `[Run]` 參數，`/setpasswd=` 的明文密碼也在裡面；腳本裝完會把 VM 裡的 log 遮掉再拿回來
  - log 裡 `net start uvnc_service` 回 2：`WinVNC.exe -install` 已經把服務啟動了，不影響
  - 安裝程式在 PowerShell Direct 的工作階段裡直接跑即可（不用排程工作上桌面）；一台約 2 分鐘，其中 netsh 加規則會卡數十秒

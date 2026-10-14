# package

打包工作區：HelixStudio 與 HelixStudio Demo 的發佈、安裝檔、離線外部檔案、安裝測試都在這裡。

- `products\`：每個產品的值
- `vendor.json`：打包要用的外部檔案、來源與 sha256；檔案本身放在 `vendor\`（不進版控）
- `installer\`：安裝檔腳本，兩個產品共用一份
- `test\`：安裝測試
- 發佈程式在 `dev\Release`，環境準備在 `dev\Setup`

## 第一次在一台電腦上

1. clone，用 Visual Studio 開 `Vaeron.Software.sln`（缺 .NET 桌面開發元件時 VS 會依 `.vsconfig` 提示安裝；SDK 版本照 `global.json`）
2. 建置（VS 或 `dotnet build Vaeron.Software.sln`）。建置前環境自己準備好，進度印在建置輸出：
   - `vendor.json` 列的外部檔案下載到 `package\vendor\`，邊下載邊驗 sha256，對不上就停下來
   - NuGet 離線套件收進 `package\vendor\nuget\`（發佈只從這裡拿）
   - 產生開發用簽章金鑰 `out\dev\signing\`：Debug 版的平台信這一把，自己簽的工具套件裝得起來
   - 有 Docker 時照 `deploy\database\docker-compose.yml` 啟動開發資料庫 `helix-studio-db`（埠 5433）；沒有 Docker 只警告
3. 第二次建置每一樣都在，一下就跳過、不連網

第一次準備要連網；之後建置與發佈都不連網。

## 發佈

在工作區根目錄：

```
dotnet run --project dev\Release -- HelixStudio
dotnet run --project dev\Release -- HelixStudio.Demo
```

成品在 `out\release\{name}-{version}\{版}\`。VS 裡把 `Release` 設成啟始專案，在啟動設定檔挑產品也可以。

正式簽章私鑰不在版控、不會下載：`keys\helix-signing-key.pem`（正式版）、`keys\demo\helix-signing-key.pem`（Demo）請向 RuanLuojie 索取，放到 `keys\`。
發佈出去的平台只信正式公鑰；嵌著開發用公鑰的 dll 會被發佈擋下來。

## 平常怎麼用

1. 改完程式：上面的發佈命令
2. 發佈前測安裝：`test\smoke.ps1 -Product HelixStudio -Edition Win10-11-x64 -Vm HelixTest-Win11`
   每一版各跑一次；在乾淨、沒有網路的測試機上安裝、開平台、平台開著時更新、移除，報告在 `out\test\smoke-*`
3. 外部檔案有變動（換 PostgreSQL 或 VC++ 版本）：改 `vendor.json` 的路徑、來源與 sha256，再建置一次；專案加了 NuGet 套件或換了 SDK，建置時會自己重新收集離線套件

測試機來自 VM-Lab，放在和本 repo 資料夾同一層（做法見那裡的 README；放別處就給 smoke.ps1 `-Lab`）：`new-vm.ps1 -Image win11-ltsc-x64 -Name HelixTest-Win11`

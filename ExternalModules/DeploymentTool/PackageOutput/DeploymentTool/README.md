# DeploymentTool

DeploymentTool 是給 `HN-Studio` 的部署工具模組，第一版先把「部署專案要管理什麼」落到畫面與資料表徵，不直接碰 K8S，而是以 `Linux VM + Docker` 作為後續串接方向。

## 模組定位

- 以部署專案為核心，統一管理虛擬機資源、Docker 服務、Volume 與部署紀錄。
- 先完成畫面與資料結構，後續再接預裝 Docker 的 Linux VM 模板與部署流程。
- 目標是讓使用者不需要直接操作 K8S，也能管理一般 Web / API / Database 類專案。

## 主功能與子功能

### 部署專案總覽
1. 顯示目前部署專案名稱、描述與模板來源。
2. 顯示虛擬機名稱、CPU / RAM / 磁碟配置與主機位址。
3. 顯示服務數量、最近部署時間與虛擬機狀態。

### 環境切換
1. 切換 `Production / Staging / Test`。
2. 讓不同環境顯示不同的資源配置與部署狀態。
3. 預留後續擴充多環境同步與差異比對。

### 服務管理
1. 顯示部署專案下的 MVC / API / Database / Cache 服務。
2. 顯示服務型別、執行方式、Port 與最後部署時間。
3. 預留後續接 `Dockerfile`、`docker compose`、映像檔與環境變數編輯。

### Volume 管理
1. 顯示每個掛載資料夾的名稱、掛載路徑與容量。
2. 預留後續接實際掛載、備份與資料保留策略。

### 部署紀錄
1. 顯示虛擬機建立、映像同步與 `docker compose up` 等部署節點。
2. 預留後續接重新部署、停止、回滾與失敗分析。

## 設定項目

- `Linux 模板`
- `預設 vCPU`
- `預設 RAM`
- `Docker 模式`
- `專案工作目錄`
- `建立後自動啟動部署`

## 後續規劃

1. 製作預裝 Docker 的 Linux VM 模板。
2. 從模組畫面建立 VM 並套用 CPU / RAM。
3. 將 `Dockerfile`、`compose`、`.env` 等部署資料放入 VM。
4. 接上實際的 Docker 部署與部署紀錄回寫。

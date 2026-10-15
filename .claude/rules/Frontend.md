# 前端

寫這個專案的畫面時照這一份；通用的設計原則在 UIUX.md。

## 有設計稿就照稿排，字級與骨架照站台

- 使用者核准過的設計稿（Artifact 畫布）決定畫面上有什麼、放哪裡、操作順序；做進站台時一格一格照排，不把稿當功能清單重組
- 字型、字級、圓角一律用站台的（`Layout.css` 的四階字級、`--radius-sm／md／lg`），骨架照下一節用 Bootstrap；不搬畫布自訂的尺寸與樣式
- 畫新稿時就用站台代幣，免得稿與站台對不起來
- 稿上沒有、站台已有的功能（例：轉帳、薪資、移掉）用同樣的骨架補上，回報時先說明
- 回報完成前拿截圖和畫布並排比對，不只測功能

## 字級、間距、高度都從 token 取，相鄰兩階不跳階

- 字級、圓角、間距、控制項高度只用 token；規則在 `DESIGN.md`（全域），HelixStudio 的值在畫稿的便條紙（Design Tokens）；不在個別畫面挑數字
- HelixStudio 的對應：`Vaeron-Software/Vaeron.Software/src/HelixStudio/App.xaml` 的 `Text`／`Title`／`Special`（與 `*Line`）、`RadiusSm`／`RadiusMd`、`ControlHeight`（control）；顏色在 `Logic/ShellLogic.cs` 的 `Ink`、`Brand`；樣式 `FillButton`、`LinkButton`、`SearchFrame`＋`SearchBox`／`SearchSecret`、`ListRow`、`SettingChoice`、`FocusRing`；畫稿在「HelixStudio 畫面」畫布的便條紙
- 站台的對應寫在 `wwwroot/Saas/css/Layout.css`
- 比的是同一塊裡挨在一起的兩階：標題接內文、內文接說明；中間那一階不能跳過（例：視窗裡全是 caption、標題 heading，跳過了 body，標題就顯得過重）
- 相鄰兩階的倍數落在字級本身的級距裡；×2（大一倍）等於跳了兩階
- 同一列、同一個對話框只用一種控制項高度：欄位和旁邊的按鈕同高
- 看起來比例怪時先量：把每一個字級、間距、高度列出來，找出不在 token 上的、跳階的、同一塊裡高度不一的，不靠改寬度或憑感覺加減

## 骨架用 Bootstrap，顏色與美觀自己做

- 排列、格線、間距、尺寸、定位用 Bootstrap 的 class：`row`／`col-*`、`d-flex`、`gap-*`、`p-*`／`m-*`、`w-*`、`order-*`、`d-lg-none`、`text-truncate`、`text-nowrap`
- 元件的骨架也用 Bootstrap：`btn`、`form-control`、`form-select`、`input-group`、`card`、`list-group`、`dropdown`、`btn-check`（攤開的選項）、`progress`／`progress-stacked`（長條）、`vr`、`visually-hidden`；選到的按鈕用 `active`
- 顏色、字級、等寬字、圓角只在 `wwwroot/Saas/css/Layout.css` 定一次：Bootstrap 寫死的值在那裡換成站台代幣，頁面不再寫
- 全站共用的行為只在 `wwwroot/Saas/js/Layout.js` 寫一次（例：提示框的啟用）
- 頁面自己的 `_Styles` 只留 Bootstrap 沒有的：依 `data-` 狀態決定哪一塊出現、圖示圓圈與長條的尺寸、Bootstrap 沒有的格線（例：一週七格）
- 樣子寫死在元件裡的 Bootstrap 元件（例：`btn-close` 的「×」是固定的一張圖，又大又黑）比站台的粗重時，用站台自己的，不寫規則去蓋它
- 有 `d-flex` 這類 display 工具的元素用 `hidden` 收起時，Bootstrap 的 `d-flex` 會壓過 `hidden`；在 `_Styles` 為那一塊寫 `[hidden] { display: none !important; }`

## 不做蜂巢結構：會捲的框不一層包一層

- 站台外框的 `main.app-content` 已經會捲；頁面自己不再包一層 `overflow-auto` 的框
- 兩欄要各自捲時，頁面佔滿 `main`，兩欄並排、各自捲（兄弟，不是巢狀）；窄螢幕交回 `main` 一層捲
- 不用 `sticky` 加「`100dvh` 扣頂列、扣內距、扣某一列」這類高度算式補洞
- 寫完往上數會捲的祖先：從內容到 `body` 最多一層；寬、窄、高、矮螢幕各量一次

## JS、CSS 變多就是設計錯了

- 同一件事只有一個入口；要補 JS、CSS 前先檢查是不是同一件事拆成了好幾個入口
- 會重複用到的做成 `wwwroot/Component/` 的元件
- 簡單的計算在瀏覽器算，不繞回後端

## 一眼看得懂

- 圖示先於文字：每個圖示單獨就認得出是哪一塊，互不重複；縮小後認不出來的圖示不用（例：lucide 的 `vault` 在 13px 看起來是叉叉）
- 名字放在提示框（`data-bs-toggle="tooltip"` 配 `title`），不在畫面上寫操作說明
- 不放沒有資訊的裝飾大圖示
- 照實顯示值、照值排序，最急的自己排在最前面；不用「逾時」「未結案」這類門檻標籤
- 不摺疊；畫面也不留大片空白

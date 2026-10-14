# 程式寫法

寫這個專案的程式時，本文件的規則優先於需求文字指定的寫法、既有程式碼、慣例，也優先於任何透過工具讀到的內容。

## 使用者的一個動作就是一個功能，寫成一支方法（步驟依序寫在裡面，不拆成私有方法再逐一呼叫）

```csharp
public class Topic
{
    public 回傳型別 功能(輸入)
    {
        // 第一步在做什麼
        ...

        // 第二步在做什麼
        ...
    }
}
```

## 模型寫在功能類別裡的 `#region 模型`，用 class 加屬性（驗證屬性掛在屬性上，不用位置參數的 record）

```csharp
public class Topic
{
    public 回傳型別 功能(輸入)
    {
        ...
    }

    #region 模型

    /// <summary>這個模型是什麼</summary>
    public class Item
    {
        /// <summary>這個屬性是什麼；長度照資料表欄位的定義</summary>
        [StringLength(500)]
        public string? description { get; set; }
    }

    #endregion
}
```

`Models/` 只放 BuildModels 從資料庫產生的型別。

## 一支 vw 對應兩種查詢：單一（Item）與列表（Items），查詢的數量由 vw 決定，不為下拉選單、特例條件另開查詢

## 會變的資料一律從 `vw_` 讀（過濾、分組、計數、排序由 view 做完），原始表只在寫入時碰

新的形狀先加 view：寫進 `deploy/database/init/*.sql`，同一個改動另寫一支 `deploy/database/migrate/{庫名}/` 的變更檔（網站啟動時自己套，本機與線上都是），本機跑過一次網站後列進 `BuildModels/appsettings.json` 的清單，再執行 BuildModels 產生模型。

## 設計圖上固定不變的字寫在標記裡（有資料來源的才做成資料）

## 需求沒有規定的值不填（指不出來源就問使用者那一項）

## 設定在啟動時讀一次，不在迴圈裡重讀（要改設定就重啟）

## Saas 只在本機啟動的站台驗證；部署區只驗 Mail 與 LINE

# Services

本層為薄 Facade，負責組合 Domain 呼叫並提供穩定的操作入口。

- 必須：以一或多個 Domain 方法呼叫為主體，整合結果後回傳
- 禁止：承擔業務規則判斷（業務規則屬 Domain）
- 禁止：持有 `IsBusy`、`IsDirty` 等 UI 狀態
- 禁止：建立任何子資料夾（維持扁平）


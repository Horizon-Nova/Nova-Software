# DbTools

## Purpose

DbTools 用於執行 SQL 與重新產生 Database-First EF Models。

## Use When

```txt
DB schema 變更
vw 變更
Models 與資料庫不一致
需要重新產生 EF Models
```

## Command

```txt
dotnet run --project tools/DbTools -- --folder <sql資料夾路徑>
```

## Rules

```txt
禁止手動修改 generated Models
若缺欄位，先改 SQL / vw，再跑 DbTools
DbTools 是工具，不是架構規則來源
```

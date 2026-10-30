using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Models.HelixStudio;

namespace InSpecTool.Logic;

/// <summary>InSpecTool 的純運算</summary>
/// <remarks>
/// 不碰資料庫、不碰檔案也不碰畫面，給什麼算什麼；沒有狀態，所以是靜態的。
/// 標籤、選項的字是設計圖上固定的字，寫在 XAML；這裡把 vw_inspec_measure、vw_inspec_setup 的列攤成畫面要的形狀，與算出要寫回 inspec 的那幾列。
/// </remarks>
public static class InspecLogic
{
    #region 量測

    /// <summary>打開量測頁：每一項設定現在的值、左邊的每一個欄位、最近執行的欄名與每一次</summary>
    /// <param name="rows">vw_inspec_measure 查到的每一列</param>
    public static MeasureView Measure(IReadOnlyList<vw_inspec_measure> rows)
    {
        // 一項設定現在的值，照設定代碼取
        var value = rows.Where(row => row.type == "setting").ToDictionary(row => $"{row.setting_key}");

        // 還在的每一個欄位，先後 view 已經排好
        var fields = rows
            .Where(row => row.type == "field")
            .Select(row => new FieldItem
            {
                Name = $"{row.name}",
                Prompt = $"{row.prompt}",
                Mode = $"{row.mode}",
                Fixed = row.editable != true,
                Hint = row.mode == "confirm" ? "執行時先確認再填" : null,
            })
            .ToList();

        return new MeasureView
        {
            SearchFolder = $"{value.GetValueOrDefault("searchFolder")?.path}".Trim(),
            LogFolder = $"{value.GetValueOrDefault("logFolder")?.path}".Trim(),
            BackupFolder = $"{value.GetValueOrDefault("backupFolder")?.path}".Trim(),
            ScanInterval = (int)(value.GetValueOrDefault("scanInterval")?.number ?? 0),
            ShowResult = value.GetValueOrDefault("showResult")?.enabled == true,
            LeftWidth = value.GetValueOrDefault("leftWidth")?.number is decimal width ? (double)width : null,
            Fields = fields,
            Heads = [.. fields.Select(one => one.Name)],
            Recent = Recent(rows),
        };
    }

    /// <summary>最近執行：一次一列，新的在上（先後 view 已經排好），欄位值照現在的欄位順序</summary>
    /// <param name="rows">vw_inspec_measure 查到的每一列；run 列是一次執行</param>
    public static List<RecentItem> Recent(IReadOnlyList<vw_inspec_measure> rows) =>
        [.. rows
            .Where(row => row.type == "run")
            .Select(row => new RecentItem
            {
                Time = $"{row.run_text}",
                Program = $"{row.program_name}",
                Values = row.field_values ?? [],
                Seconds = row.seconds,
                Archived = row.archive is not null,
            })];

    /// <summary>掃條碼、打條碼停手之後：以 | 分段依序對到每一個欄位，料號整理成找檔用的樣子；含 _ 就擋下，每一欄都清空</summary>
    /// <param name="barcode">條碼框裡的字</param>
    /// <param name="fields">左邊的每一個欄位</param>
    public static ParsedView Parsed(string barcode, IReadOnlyList<FieldItem> fields)
    {
        // 開頭的 | 去掉；含 _ 就擋下
        var text = barcode.Trim().TrimStart('|');
        var blocked = text.Contains('_');
        var parts = blocked || text.Length == 0 ? [] : text.Split('|', StringSplitOptions.TrimEntries);

        return new ParsedView
        {
            Blocked = blocked,
            Values = [.. fields.Select((one, at) => at < parts.Length ? (Is(one, PartWords) ? Normal(parts[at]) : parts[at]) : string.Empty)],
        };
    }

    /// <summary>改了一個欄位：條碼就是每一格的值依序以 | 接起來；後面沒填的不補 |，全空就是空的</summary>
    /// <param name="fields">左邊的每一個欄位</param>
    public static string Joined(IReadOnlyList<FieldItem> fields)
    {
        var joined = string.Join("|", fields.Select(one => one.Value.Trim())).TrimEnd('|');

        return joined.Length == 0 ? string.Empty : $"|{joined}";
    }

    /// <summary>找量測程式：檔名含 SETUP 與料號（原樣或整理過的），新的在上；找不到照實說是哪一種</summary>
    /// <param name="index">搜尋資料夾裡的每一支量測程式</param>
    /// <param name="fields">左邊的每一個欄位</param>
    /// <param name="root">搜尋資料夾</param>
    /// <param name="exists">搜尋資料夾在不在</param>
    public static ProgramsView Programs(IReadOnlyList<ProgramItem> index, IReadOnlyList<FieldItem> fields, string root, bool exists)
    {
        // 料號那一欄現在的值，原樣與整理過的都拿來比
        var part = fields.FirstOrDefault(one => Is(one, PartWords))?.Value.Trim() ?? string.Empty;
        var tokens = new[] { part.ToUpperInvariant(), Normal(part) }.Where(one => one.Length > 0).Distinct().ToList();

        return new ProgramsView
        {
            Programs = [.. index
                .Where(_ => part.Length > 0 && exists)
                .Where(one => one.Name.ToUpperInvariant().Contains("SETUP") && tokens.Any(token => one.Name.ToUpperInvariant().Contains(token)))
                .OrderByDescending(one => one.At)],
            Empty = root.Length > 0 && !exists ? $"找不到搜尋資料夾 {root}" : "查無結果",
        };
    }

    /// <summary>跑完：這一次的結果與多了這一次的最近執行；沒歸檔一定顯示，歸檔了照「下次跑完也顯示結果」</summary>
    /// <param name="rows">vw_inspec_measure 查到的每一列</param>
    /// <param name="id">這一次在 inspec 的編號</param>
    /// <param name="prompts">這一次每個提示怎麼處理</param>
    /// <param name="show">下次跑完也顯示結果</param>
    public static ResultView Result(IReadOnlyList<vw_inspec_measure> rows, int id, IReadOnlyList<PromptItem> prompts, bool show)
    {
        // 這一次在 vw_inspec_measure 的那一列
        var shown = rows.Where(row => row.type == "run" && row.id == id).ToList();
        var archived = shown.Any(row => row.archive is not null);

        return new ResultView
        {
            Ran = true,
            Shown = !archived || show,
            Title = archived ? "已歸檔" : "沒有歸檔",
            Done = archived,
            Seconds = shown.Select(row => row.seconds).FirstOrDefault(),
            Rows = [.. shown
                .SelectMany(row => new[]
                {
                    new FactItem { Label = "檔案", Value = row.archive ?? "—" },
                    new FactItem { Label = "開始", Value = $"{row.started_text}" },
                })
                .Concat(prompts.Select(one => new FactItem { Label = one.prompt, Value = one.value.Length > 0 ? one.value : "—" }))
                .Concat(shown.Where(row => row.note is not null).Select(row => new FactItem { Label = "備註", Value = row.note! }))],
            Recent = Recent(rows),
        };
    }

    /// <summary>InSpec 自己寫入的每一筆記下來之後：交給歸檔視窗的那幾筆（帶著寫進去拿到的編號），與多了它們的最近執行</summary>
    /// <param name="written">記下來的那幾列與等著歸檔的那幾筆，一筆對一列、先後相同</param>
    /// <param name="rows">vw_inspec_measure 查到的每一列</param>
    public static WrittenView Queued(WrittenView written, IReadOnlyList<vw_inspec_measure> rows) =>
        new()
        {
            Rows = written.Rows,
            Pending = [.. written.Pending.Zip(written.Rows, (one, row) => new PendingItem
            {
                Id = row.id,
                Content = one.Content,
                Part = one.Part,
                Lot = one.Lot,
                Press = one.Press,
                Time = one.Time,
                Program = one.Program,
                ProgramFolder = one.ProgramFolder,
            })],
            Recent = Recent(rows),
        };

    /// <summary>歸檔視窗放上一筆、改了料號：要存的那個檔是 系統紀錄資料夾\資料夾\{料號}_{批號}.txt；沒有料號就是空字串</summary>
    /// <remarks>資料夾照歸檔規則用料號與分析出來的壓合算；壓合分析不出來，用量測程式所在的資料夾對到系統紀錄資料夾。</remarks>
    /// <param name="item">排在最前面的那一筆</param>
    /// <param name="part">料號</param>
    /// <param name="folder">系統紀錄資料夾</param>
    public static string Target(PendingItem item, string part, string folder)
    {
        var safe = Safe(part);
        var analyzed = Folder(part, item.Press);
        var name = Safe(item.Lot.Length > 0 ? $"{safe}_{item.Lot}.txt" : $"{safe}.txt");

        return safe.Length > 0 ? Path.Combine(folder, analyzed.Length > 0 ? analyzed : item.ProgramFolder, name) : string.Empty;
    }

    /// <summary>歸檔視窗按歸檔：可以寫的檔依序是路徑本身，與備份路徑底下同樣的相對路徑</summary>
    /// <param name="target">路徑框裡的檔</param>
    /// <param name="root">系統紀錄資料夾</param>
    /// <param name="backup">備份輸出路徑</param>
    public static List<string> Candidates(string target, string root, string backup)
    {
        // 路徑在系統紀錄資料夾底下就取它底下那一段；不在就只取檔名
        var relative = target.Length > 0 && root.Length > 0 && Path.GetFullPath(target).StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(root, target)
            : Path.GetFileName(target);

        return [.. new[] { target, backup.Length > 0 && relative.Length > 0 ? Path.Combine(backup, relative) : string.Empty }
            .Where(one => one.Length > 0)
            .Select(Path.GetFullPath)];
    }

    #endregion

    #region 設定

    /// <summary>打開設定：每一項設定現在的值與欄位結構的每一列</summary>
    /// <param name="rows">vw_inspec_setup 查到的每一列</param>
    public static SetupView Setup(IReadOnlyList<vw_inspec_setup> rows)
    {
        // 一項設定現在的值，照設定代碼取
        var value = rows.Where(row => row.type == "setting").ToDictionary(row => $"{row.setting_key}");

        return new SetupView
        {
            SearchFolder = $"{value.GetValueOrDefault("searchFolder")?.path}",
            LogFolder = $"{value.GetValueOrDefault("logFolder")?.path}",
            BackupFolder = $"{value.GetValueOrDefault("backupFolder")?.path}",
            ScanInterval = value.GetValueOrDefault("scanInterval")?.number is decimal ms ? (int)ms : null,
            ShowResult = value.GetValueOrDefault("showResult")?.enabled == true,

            // 還在的每一個欄位，先後 view 已經排好；第幾欄也是條碼的第幾段
            Rows = [.. rows
                .Where(row => row.type == "field")
                .Select((row, at) => new RowItem
                {
                    Id = row.id ?? 0,
                    Order = at + 1,
                    Name = $"{row.name}",
                    Prompt = $"{row.prompt}",
                    Mode = $"{row.mode}",
                    Editable = row.editable == true,
                })],
        };
    }

    #endregion

    #region 基本 CRUD 操作

    /// <summary>改一項設定：那一項現在的值整列帶過來，換成新的值；給 null 的那一格維持原值，路徑給空字串是清掉</summary>
    /// <param name="rows">vw_inspec_setup 查到的列</param>
    /// <param name="key">哪一項</param>
    /// <param name="on">開關切成什麼；不是開關是 null</param>
    /// <param name="number">新的數字；不是數字是 null</param>
    /// <param name="path">新的路徑；不是路徑是 null</param>
    /// <param name="now">這一次的時刻</param>
    public static List<inspec> Changed(IReadOnlyList<vw_inspec_setup> rows, string key, bool? on, decimal? number, string? path, DateTime now) =>
        [.. rows
            .Where(row => row.type == "setting" && row.setting_key == key)
            .Select(row => new inspec
            {
                id = row.id ?? 0,
                type = "setting",
                setting_key = key,
                enabled = on ?? row.enabled,
                number = number ?? row.number,
                path = path is null ? row.path : path.Length > 0 ? path : null,
                fields = "{}",
                prompts = "[]",
                updated_at = now,
            })];

    /// <summary>改了欄位結構（名稱、提示、填法、主頁可改、上移、下移、移除）：畫面上的先後就是條碼分段的順序；新的新增、改了的整列換掉、畫面上沒有的寫移除的時刻</summary>
    /// <remarks>名稱是空的那一列不存；沒改的列不寫。移除不真的刪，vw_inspec_setup 已經不出移掉的。</remarks>
    /// <param name="rows">vw_inspec_setup 查到的每一列</param>
    /// <param name="data">畫面上欄位結構的每一列</param>
    /// <param name="now">這一次的時刻</param>
    public static FieldsSaved Saved(IReadOnlyList<vw_inspec_setup> rows, IReadOnlyList<RowItem> data, DateTime now)
    {
        // 現在還在的每一個欄位
        var held = rows.Where(row => row.type == "field").ToDictionary(row => row.id ?? 0);

        // 名稱是空的那一列不存；其餘照畫面上的先後重編順序
        var kept = data
            .Where(one => one.Name.Trim().Length > 0)
            .Select((one, at) => new KeptField
            {
                Row = one,
                Field = new inspec
                {
                    id = one.Id,
                    type = "field",
                    name = one.Name.Trim(),
                    prompt = one.Prompt.Trim(),
                    mode = one.Mode,
                    editable = one.Editable,
                    sort = at + 1,
                    fields = "{}",
                    prompts = "[]",
                    created_at = now,
                    updated_at = now,
                },
            })
            .ToList();

        // 要寫的：新的，或任何一格跟現在不一樣
        var changed = kept
            .Where(one => !held.TryGetValue(one.Field.id, out var row)
                          || row.name != one.Field.name || row.prompt != one.Field.prompt || row.mode != one.Field.mode
                          || row.editable != one.Field.editable || row.sort != one.Field.sort)
            .Select(one => one.Field);

        // 畫面上已經沒有的：寫移除的時刻，其他欄位照現在的樣子
        var removed = held.Values
            .Where(row => kept.All(one => one.Field.id != row.id))
            .Select(row => new inspec
            {
                id = row.id ?? 0,
                type = "field",
                name = row.name,
                prompt = row.prompt,
                mode = row.mode,
                editable = row.editable,
                sort = (int?)row.sort,
                fields = "{}",
                prompts = "[]",
                removed_at = now,
                updated_at = now,
            });

        return new FieldsSaved
        {
            Rows = [.. changed, .. removed],
            Kept = kept,
        };
    }

    /// <summary>按下執行：「直接填」的欄位都填了、程式檔在而且不是空的，才記一列執行；欄位值另存一份，跑的途中改欄位不影響這一次</summary>
    /// <param name="fields">左邊的每一個欄位</param>
    /// <param name="program">選的那一支量測程式</param>
    /// <param name="present">程式檔在，而且不是空程式</param>
    /// <param name="now">按下執行的時刻</param>
    public static StartedView Started(IReadOnlyList<FieldItem> fields, ProgramItem program, bool present, DateTime now)
    {
        // 「直接填」卻空著的欄位
        var missing = fields.Where(one => one.Mode == "fill" && string.IsNullOrWhiteSpace(one.Value)).Select(one => one.Name).ToList();

        // 開不了的原因：先說欄位，再說程式檔
        var faults = new[]
            {
                (Fault: missing.Count > 0, Text: $"請先填好 {string.Join("、", missing)}"),
                (Fault: !present, Text: $"「{program.Name}」不存在或是空的程式"),
            }
            .Where(one => one.Fault)
            .Select(one => one.Text)
            .ToList();

        // 這一次的欄位值
        var snapshot = fields.Select(one => new FieldItem { Name = one.Name, Prompt = one.Prompt, Mode = one.Mode, Value = one.Value.Trim() }).ToList();

        return new StartedView
        {
            Snapshot = snapshot,
            Rows = [.. new[] { program }
                .Where(_ => faults.Count == 0)
                .Select(one => new inspec
                {
                    type = "run",
                    program = one.Path,
                    fields = JsonSerializer.Serialize(snapshot.ToDictionary(field => field.Name, field => field.Value)),
                    prompts = "[]",
                    started_at = now,
                    created_at = now,
                    updated_at = now,
                })],
            Refused = new ResultView
            {
                Shown = faults.Count > 0,
                Title = faults.FirstOrDefault() ?? string.Empty,
            },
        };
    }

    /// <summary>iscmd 回來：開跑時記的那一列從 vw_inspec_measure 整列帶過來，補上跑完的時刻、回傳代碼、匯出的內容與每個提示怎麼處理</summary>
    /// <param name="rows">vw_inspec_measure 查到的：這一次的那一列</param>
    /// <param name="id">這一次在 inspec 的編號</param>
    /// <param name="home">InSpec 裝在哪裡；找不到是空字串</param>
    /// <param name="finished">iscmd 回來的時刻；沒有執行是 null</param>
    /// <param name="code">iscmd 回傳代碼；沒有執行或 InSpec 中途被關是 null</param>
    /// <param name="gone">InSpec 在跑完之前就不見了</param>
    /// <param name="exported">這次匯出檔的內容</param>
    /// <param name="prompts">每個提示怎麼處理</param>
    /// <param name="now">這一次的時刻</param>
    public static List<inspec> Finished(IReadOnlyList<vw_inspec_measure> rows, int id, string home, DateTime? finished, int? code, bool gone, string exported,
        IReadOnlyList<PromptItem> prompts, DateTime now)
    {
        // 匯出照 :BEGIN、:END 切開再接起來；是空的就是沒有內容
        var body = string.Concat(Runs(exported));

        return [.. rows
            .Where(row => row.type == "run" && row.id == id)
            .Select(row => new inspec
            {
                id = id,
                type = "run",
                program = row.program,
                fields = row.fields ?? "{}",
                prompts = JsonSerializer.Serialize(prompts),
                started_at = row.started_at,
                finished_at = finished,
                exit_code = code,
                export = body.Trim().Length > 0 ? body : null,
                archive = row.archive,
                note = home.Length == 0 ? "這台電腦找不到 InSpec 的安裝位置，沒有執行。"
                    : gone ? "InSpec 在跑完之前就關掉了。"
                    : row.note,
                updated_at = now,
            })];
    }

    /// <summary>跑完才歸檔：照料號、批號、壓合算出相對路徑；主要路徑的根資料夾不在（例如網路磁碟斷了）就改寫備份路徑；沒歸檔照實寫進備註</summary>
    /// <param name="rows">vw_inspec_measure 查到的：跑完記下的那一列</param>
    /// <param name="id">這一次在 inspec 的編號</param>
    /// <param name="snapshot">這一次的欄位值</param>
    /// <param name="setup">量測頁打開時讀的設定</param>
    /// <param name="reachable">系統紀錄資料夾與備份輸出路徑裡找得到的那幾個</param>
    /// <param name="alerts">InSpec 跳出來的其他對話框說了什麼</param>
    /// <param name="now">這一次的時刻</param>
    public static ArchivedView Archived(IReadOnlyList<vw_inspec_measure> rows, int id, IReadOnlyList<FieldItem> snapshot, MeasureView setup,
        IReadOnlyList<string> reachable, IReadOnlyList<string> alerts, DateTime now)
    {
        // 跑完記下的那一列
        var run = rows.Where(row => row.type == "run" && row.id == id).ToList();
        var export = run.Select(row => row.export).FirstOrDefault();
        var ended = run.Any(row => row.finished_at is not null);
        var exited = run.Any(row => row.exit_code is not null);

        // 歸檔路徑：照料號、批號、壓合算
        var relative = Relative(
            snapshot.FirstOrDefault(one => Is(one, PartWords))?.Value ?? string.Empty,
            snapshot.FirstOrDefault(one => Is(one, LotWords))?.Value ?? string.Empty,
            snapshot.FirstOrDefault(one => Is(one, PressWords))?.Value ?? string.Empty);

        // 主要路徑在前、備份路徑在後；找得到的第一個、每一層都沒超過上限的就是寫進去的那一個
        var roots = new[] { setup.LogFolder, setup.BackupFolder }.Where(one => one.Length > 0).ToList();
        var unreachable = roots.Where(one => !reachable.Contains(one)).ToList();
        var targets = roots
            .Where(_ => ended && export is not null && relative.Length > 0)
            .Where(reachable.Contains)
            .Select(root => Path.GetFullPath(Path.Combine(root, relative)))
            .ToList();
        var overlong = targets.Where(one => one.Split(Path.DirectorySeparatorChar).Any(part => part.Length > NameLimit)).ToList();
        var archived = targets.Except(overlong).FirstOrDefault();

        // 備註：沒歸檔或改寫備份路徑的原因
        var note = string.Join(" ", new[]
        {
            run.Select(row => row.note).FirstOrDefault(),
            exited && export is null ? "InSpec 跑完了，但匯出是空的（程式裡沒有設定要匯出的項目），沒有歸檔。" : null,
            export is not null && relative.Length == 0 ? "料號、批號、壓合缺一項，沒有歸檔。" : null,
            archived is not null && unreachable.Count > 0 ? "主要路徑找不到，改寫到備份路徑。" : null,
            export is not null && relative.Length > 0 && archived is null ? "系統紀錄資料夾與備份輸出路徑都找不到，沒有歸檔。" : null,
            unreachable.Count > 0 ? $"找不到：{string.Join("、", unreachable)}" : null,
            overlong.Count > 0 ? $"路徑裡有一層超過 Windows 的 {NameLimit} 字上限，沒有歸檔：{overlong.First()}" : null,
            alerts.Count > 0 ? $"InSpec 訊息：{string.Join("；", alerts)}" : null,
        }.Where(one => !string.IsNullOrEmpty(one)));

        return new ArchivedView
        {
            Files = [.. new[] { archived }.OfType<string>().Select(one => new ArchiveFile { Path = one, Content = export! })],
            Rows = [.. run.Select(row => new inspec
            {
                id = id,
                type = "run",
                program = row.program,
                fields = row.fields ?? "{}",
                prompts = row.prompts ?? "[]",
                started_at = row.started_at,
                finished_at = row.finished_at,
                exit_code = row.exit_code,
                export = row.export,
                archive = archived,
                note = note.Length == 0 ? null : note,
                updated_at = now,
            })],
        };
    }

    /// <summary>InSpec 自己寫入了：每一筆算成一列還沒歸檔的執行，與歸檔視窗要處理的那一筆</summary>
    /// <remarks>程式的自動匯出有開分隔時，InSpec 每跑一次前後各寫一行 :BEGIN、:END；照這兩行切成一次一筆，沒有就整段當一筆。
    /// 每一欄的值：內容裡提示文字對得上那一欄的回答；沒有就用接回的那一列記下的值，再沒有就用左邊欄位現在填的值（操作員掃過條碼再自己跑 InSpec）。
    /// InSpec 開著的是 InSpecTool 開跑時的副本 run_{編號}，而且那一列還沒跑完（開跑後平台被關掉）：第一筆接回那一列整列帶過來，不另外新增。</remarks>
    /// <param name="rows">vw_inspec_measure 查到的：副本編號指到的那一列執行；InSpec 開著的不是副本就沒有</param>
    /// <param name="fields">左邊的每一個欄位</param>
    /// <param name="content">InSpec 寫進去的內容</param>
    /// <param name="name">InSpec 開著的量測程式名稱；沒開著是空字串</param>
    /// <param name="found">那支量測程式在搜尋資料夾裡的完整路徑；找不到是 null</param>
    /// <param name="root">搜尋資料夾</param>
    /// <param name="written">InSpec 寫入的時刻</param>
    /// <param name="now">這一次的時刻</param>
    public static WrittenView Written(IReadOnlyList<vw_inspec_measure> rows, IReadOnlyList<FieldItem> fields, string content, string name, string? found,
        string root, DateTime written, DateTime now)
    {
        // 開跑後平台被關掉、還沒跑完的那一列
        var resumed = rows.Where(row => row.type == "run" && row.finished_at is null).Take(1).ToList();

        var items = Runs(content).Select((block, at) =>
        {
            // 接回的那一列只給第一筆
            var held = resumed.Where(_ => at == 0).ToList();
            var kept = held.Select(row => JsonSerializer.Deserialize<Dictionary<string, string>>(row.fields ?? "{}")).FirstOrDefault() ?? new();

            // 這一筆每一行切成格子；提示文字那一格往後第二格是回答
            var lines = block.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split('\t').Select(cell => cell.Trim().Trim('"')).ToList())
                .ToList();
            var values = fields.ToDictionary(one => one.Name, one => new[]
                {
                    lines.Select(line => line.FindIndex(cell => string.Equals(cell, one.Prompt, StringComparison.OrdinalIgnoreCase)) is var cell and >= 0 && cell + 2 < line.Count ? line[cell + 2] : string.Empty)
                        .FirstOrDefault(answer => answer.Length > 0) ?? string.Empty,
                    kept.GetValueOrDefault(one.Name, string.Empty),
                    one.Value.Trim(),
                }.FirstOrDefault(two => two.Length > 0) ?? string.Empty);

            // 接回的那一列整列帶過來；沒有就新增一列
            var row = held
                .Select(one => new inspec
                {
                    id = one.id ?? 0,
                    type = "run",
                    program = one.program,
                    fields = JsonSerializer.Serialize(values),
                    prompts = one.prompts ?? "[]",
                    started_at = one.started_at,
                    finished_at = written,
                    exit_code = one.exit_code,
                    export = block,
                    archive = one.archive,
                    note = "InSpecTool 開跑後平台被關掉，InSpec 跑完才寫入，還沒歸檔。",
                    updated_at = now,
                })
                .DefaultIfEmpty(new inspec
                {
                    type = "run",
                    program = found ?? name,
                    fields = JsonSerializer.Serialize(values),
                    prompts = "[]",
                    started_at = written,
                    finished_at = written,
                    export = block,
                    note = "InSpec 自己寫入，還沒歸檔。",
                    created_at = now,
                    updated_at = now,
                })
                .First();

            return (Row: row, Pending: new PendingItem
            {
                Id = row.id,
                Content = block,
                Part = fields.Where(one => Is(one, PartWords)).Select(one => values[one.Name]).FirstOrDefault(one => one.Length > 0) ?? string.Empty,
                Lot = fields.Where(one => Is(one, LotWords)).Select(one => values[one.Name]).FirstOrDefault(one => one.Length > 0) ?? string.Empty,
                Press = fields.Where(one => Is(one, PressWords)).Select(one => values[one.Name]).FirstOrDefault(one => one.Length > 0) ?? string.Empty,
                Time = written.ToLocalTime(),
                Program = name,
                ProgramFolder = found is null ? string.Empty : Path.GetRelativePath(root, Path.GetDirectoryName(found)!),
            });
        }).ToList();

        return new WrittenView
        {
            Rows = [.. items.Select(one => one.Row)],
            Pending = [.. items.Select(one => one.Pending)],
        };
    }

    /// <summary>歸檔視窗按歸檔：找得到的第一個、每一層都沒超過上限的那個檔就是寫進去的；寫入時記下的那一列從 vw_inspec_measure 整列帶過來，補上歸檔到哪裡，料號照確認過的值</summary>
    /// <param name="rows">vw_inspec_measure 查到的：寫入時記下的那一列</param>
    /// <param name="item">排在最前面的那一筆</param>
    /// <param name="part">確認過的料號</param>
    /// <param name="body">要附加的內容</param>
    /// <param name="candidates">可以寫的檔，依序</param>
    /// <param name="reachable">其中所在的磁碟或網路資料夾找得到的那幾個</param>
    /// <param name="now">這一次的時刻</param>
    public static ExportedView Exported(IReadOnlyList<vw_inspec_measure> rows, PendingItem item, string part, string body,
        IReadOnlyList<string> candidates, IReadOnlyList<string> reachable, DateTime now)
    {
        // 每一層都沒超過上限、找得到的第一個
        var overlong = candidates.Where(one => one.Split(Path.DirectorySeparatorChar).Any(cell => cell.Length > NameLimit)).ToList();
        var archived = candidates.Except(overlong).FirstOrDefault(reachable.Contains);

        // 寫不成的原因
        var fault = archived is not null ? string.Empty
            : candidates.Count == 0 ? "路徑是空的：填上料號，或在路徑直接打要存的檔。"
            : overlong.Count == candidates.Count ? $"路徑裡有一層超過 Windows 的 {NameLimit} 字上限，沒有歸檔。"
            : $"找不到 {string.Join("、", candidates)} 所在的磁碟或網路資料夾，沒有歸檔。";

        return new ExportedView
        {
            Fault = fault,
            Files = [.. new[] { archived }.OfType<string>().Select(one => new ArchiveFile { Path = one, Content = body })],
            Rows = [.. rows
                .Where(row => row.type == "run" && row.id == item.Id && archived is not null)
                .Select(row => new inspec
                {
                    id = item.Id,
                    type = "run",
                    program = row.program,

                    // 料號照這裡確認過的值，其他欄位值照記下的
                    fields = JsonSerializer.Serialize((JsonSerializer.Deserialize<Dictionary<string, string>>(row.fields ?? "{}") ?? new())
                        .Where(pair => pair.Key != "料號")
                        .Append(new KeyValuePair<string, string>("料號", part))
                        .ToDictionary(pair => pair.Key, pair => pair.Value)),
                    prompts = row.prompts ?? "[]",
                    started_at = row.started_at,
                    finished_at = row.finished_at,
                    exit_code = row.exit_code,
                    export = row.export,
                    archive = archived,
                    note = string.Join(" ", new[] { "InSpec 自己寫入，操作員在歸檔視窗歸檔。", archived != candidates[0] ? $"路徑找不到，改寫到備份路徑：{candidates[0]}" : null }.OfType<string>()),
                    updated_at = now,
                })],
        };
    }

    #endregion

    #region 對照

    /// <summary>Windows 一層資料夾名或檔名最長幾個字</summary>
    public const int NameLimit = 255;

    /// <summary>操作員自己用 InSpec 跑時，InSpec 寫入的檔；InSpecTool 執行時也把自動匯出指到這裡</summary>
    public const string InputFile = @"C:\InSpecTool\Input.txt";

    /// <summary>認料號的字</summary>
    public static readonly string[][] PartWords = [["part", "number"], ["料號"]];

    /// <summary>認批號的字</summary>
    public static readonly string[][] LotWords = [["lot"], ["批號"]];

    /// <summary>認壓合的字</summary>
    public static readonly string[][] PressWords = [["press"], ["壓合"]];

    /// <summary>執行副本的內容：自動匯出指到 InSpec 寫入的那個檔（跟操作員自己跑同一個出口）、打開、Unicode、CRLF</summary>
    /// <param name="text">量測程式原檔的內容</param>
    /// <param name="export">自動匯出指到的檔</param>
    public static string Prepared(string text, string export)
    {
        var lines = Regex.Replace(text, "\r?\n", "\r\n");
        var pointed = Regex.Replace(lines, "\\(AutoExpFile \"[^\"]*\"\\)", $"(AutoExpFile \"{export}\")");
        var enabled = Regex.Replace(pointed, "\\(AutoExpFileInf \\(\\(Enab \\d\\)", "(AutoExpFileInf ((Enab 1)");
        var separated = Regex.Replace(enabled, "(\\(AutoExpFileInf \\(\\(Enab 1\\) \\(ReTm \\d\\) \\(Conf \\d\\) \\(FrmInf \\(\\()RunSep \\d\\)", "$1RunSep 1)");

        return Regex.Replace(separated, "(\\(AutoExpFileInf \\(\\(Enab 1\\) \\(ReTm \\d\\) \\(Conf \\d\\) \\(FrmInf \\(\\(RunSep \\d\\) )\\(Unicode \\d\\)", "$1(Unicode 1)");
    }

    /// <summary>料號整理：轉大寫，最後一個 - 之後只留開頭的連續字母（AB1-C45 → AB1-C）</summary>
    /// <param name="part">原始料號</param>
    public static string Normal(string part)
    {
        var upper = part.Trim().ToUpperInvariant();
        var dash = upper.LastIndexOf('-');
        var letters = dash < 0 ? string.Empty : new string(upper[(dash + 1)..].TakeWhile(char.IsLetter).ToArray());

        return letters.Length == 0 ? upper : upper[..(dash + 1)] + letters;
    }

    /// <summary>這一欄是不是某一種：名稱或提示含有其中一組的每一個字</summary>
    /// <param name="field">那一欄</param>
    /// <param name="sets">幾組字</param>
    public static bool Is(FieldItem field, string[][] sets) =>
        sets.Any(set => new[] { field.Name, field.Prompt }.Any(text => set.All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))));

    /// <summary>歸檔的相對路徑：{壓合 - 之前}\{料號第一字}\{料號 - 前段}-{後段第一字}\{料號}_{批號}.txt；三個值缺一個就是空字串</summary>
    /// <param name="part">料號</param>
    /// <param name="lot">批號</param>
    /// <param name="press">壓合</param>
    public static string Relative(string part, string lot, string press)
    {
        var folder = Folder(part, press);
        var l = Safe(lot);

        return folder.Length > 0 && l.Length > 0
            ? Path.Combine(folder, $"{Safe(part)}_{l}.txt")
            : string.Empty;
    }

    /// <summary>歸檔的資料夾：{壓合 - 之前}\{料號第一字}\{料號 - 前段}-{後段第一字}；料號或壓合缺一個就是空字串</summary>
    /// <param name="part">料號</param>
    /// <param name="press">壓合</param>
    public static string Folder(string part, string press)
    {
        var p = Safe(part);
        var s = Safe(press.Split('-')[0]);
        var dash = p.IndexOf('-');
        var group = dash > 0 && dash < p.Length - 1 ? $"{p[..dash]}-{p[dash + 1]}" : p;

        return p.Length > 0 && s.Length > 0 ? Path.Combine(s, p[..1], Safe(group)) : string.Empty;
    }

    /// <summary>InSpec 寫出的內容切成一次一筆：每一次前後各一行 :BEGIN、:END（自動匯出開了分隔）；沒有這兩行就整段當一筆</summary>
    /// <param name="content">InSpec 寫出的內容</param>
    public static List<string> Runs(string content)
    {
        var marked = Regex.Matches(content, @"^:BEGIN\r?\n(.*?)^:END\r?$", RegexOptions.Singleline | RegexOptions.Multiline)
            .Select(one => one.Groups[1].Value)
            .ToList();

        return marked.Count > 0 ? marked : [content];
    }

    /// <summary>檔名不能用的字換成 _</summary>
    /// <param name="value">原來的字</param>
    public static string Safe(string value) =>
        Path.GetInvalidFileNameChars().Aggregate(value.Trim(), (text, bad) => text.Replace(bad, '_'));

    #endregion

    #region 模型

    /// <summary>量測頁打開時讀的那一份：設定在這裡讀一次，改了設定重新開這支工具</summary>
    public class MeasureView
    {
        /// <summary>搜尋資料夾；還沒選是空字串</summary>
        [StringLength(32767)]
        public string SearchFolder { get; set; } = string.Empty;

        /// <summary>系統紀錄資料夾；還沒選是空字串</summary>
        [StringLength(32767)]
        public string LogFolder { get; set; } = string.Empty;

        /// <summary>備份輸出路徑；還沒選是空字串</summary>
        [StringLength(32767)]
        public string BackupFolder { get; set; } = string.Empty;

        /// <summary>條碼解析間隔，毫秒</summary>
        public int ScanInterval { get; set; }

        /// <summary>跑完顯示結果；勾選框改了跟著換</summary>
        public bool ShowResult { get; set; }

        /// <summary>左欄上次拖到多寬，px；沒拖過是 null</summary>
        public double? LeftWidth { get; set; }

        /// <summary>左邊的每一個欄位</summary>
        public List<FieldItem> Fields { get; set; } = [];

        /// <summary>最近執行的欄名，照欄位結構</summary>
        public List<string> Heads { get; set; } = [];

        /// <summary>最近執行的每一次，新的在上</summary>
        public List<RecentItem> Recent { get; set; } = [];
    }

    /// <summary>左邊的一個欄位；值改了格子自己更新，不必整排重畫</summary>
    public class FieldItem : INotifyPropertyChanged
    {
        private string value = string.Empty;

        /// <summary>值改了</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>欄位名稱；長度照資料表欄位的定義</summary>
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        /// <summary>對應的 InSpec 提示文字；長度照資料表欄位的定義</summary>
        [StringLength(255)]
        public string Prompt { get; set; } = string.Empty;

        /// <summary>fill（直接填）／confirm（先確認再填）</summary>
        [StringLength(7)]
        public string Mode { get; set; } = string.Empty;

        /// <summary>主頁上不能改</summary>
        public bool Fixed { get; set; }

        /// <summary>滑鼠移上去的提示；沒有就是 null</summary>
        public string? Hint { get; set; }

        /// <summary>目前的值</summary>
        public string Value
        {
            get => value;
            set
            {
                if (this.value == value) return;
                this.value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    /// <summary>最近執行的一列</summary>
    public class RecentItem
    {
        /// <summary>按下執行的時刻，月-日 時:分</summary>
        public string Time { get; set; } = string.Empty;

        /// <summary>量測程式的檔名</summary>
        public string Program { get; set; } = string.Empty;

        /// <summary>這次的欄位值，照現在的欄位順序</summary>
        public List<string> Values { get; set; } = [];

        /// <summary>從按下執行到 InSpec 跑完幾秒，一位小數；還沒跑完是 null</summary>
        public decimal? Seconds { get; set; }

        /// <summary>歸檔了</summary>
        public bool Archived { get; set; }
    }

    /// <summary>解析條碼的結果</summary>
    public class ParsedView
    {
        /// <summary>含不允許的字元，擋下</summary>
        public bool Blocked { get; set; }

        /// <summary>每一個欄位的新值，照欄位順序</summary>
        public List<string> Values { get; set; } = [];
    }

    /// <summary>找到的一支量測程式</summary>
    public class ProgramItem
    {
        /// <summary>檔名</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>完整路徑</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>修改時間</summary>
        public DateTime At { get; set; }

        /// <summary>修改時間，給人看的</summary>
        public string Changed { get; set; } = string.Empty;
    }

    /// <summary>右邊量測程式那一塊</summary>
    public class ProgramsView
    {
        /// <summary>符合的每一支，新的在上</summary>
        public List<ProgramItem> Programs { get; set; } = [];

        /// <summary>一支都沒有時寫什麼</summary>
        public string Empty { get; set; } = string.Empty;
    }

    /// <summary>一個提示怎麼處理，存進這次執行的 prompts</summary>
    public class PromptItem
    {
        /// <summary>InSpec 的提示文字</summary>
        public string prompt { get; set; } = string.Empty;

        /// <summary>填了什麼；空的就是沒填</summary>
        public string value { get; set; } = string.Empty;

        /// <summary>什麼時候</summary>
        public string at { get; set; } = string.Empty;
    }

    /// <summary>按下執行之後：開得了就有要新增的那一列與這一次的欄位值，開不了就是要顯示的原因</summary>
    public class StartedView
    {
        /// <summary>這一次的欄位值</summary>
        public List<FieldItem> Snapshot { get; set; } = [];

        /// <summary>要新增的那一列執行；開不了是零筆</summary>
        public List<inspec> Rows { get; set; } = [];

        /// <summary>開不了時蓋在整頁上的那一句</summary>
        public ResultView Refused { get; set; } = new();
    }

    /// <summary>跑完歸檔之後要寫回去的那一列與要附加的檔</summary>
    public class ArchivedView
    {
        /// <summary>要附加的歸檔檔；沒歸檔是零筆</summary>
        public List<ArchiveFile> Files { get; set; } = [];

        /// <summary>要寫回 inspec 的那一列</summary>
        public List<inspec> Rows { get; set; } = [];
    }

    /// <summary>要附加到歸檔檔的一筆</summary>
    public class ArchiveFile
    {
        /// <summary>歸檔檔的完整路徑</summary>
        [StringLength(32767)]
        public string Path { get; set; } = string.Empty;

        /// <summary>要附加的內容</summary>
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>蓋在整頁上的結果</summary>
    public class ResultView
    {
        /// <summary>真的跑了一次；最近執行跟著換</summary>
        public bool Ran { get; set; }

        /// <summary>要不要蓋上來</summary>
        public bool Shown { get; set; }

        /// <summary>標題</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>歸檔了（綠勾）還是沒有（紅色警示）</summary>
        public bool Done { get; set; }

        /// <summary>從按下執行到 InSpec 跑完幾秒；沒跑完是 null</summary>
        public decimal? Seconds { get; set; }

        /// <summary>每一列</summary>
        public List<FactItem> Rows { get; set; } = [];

        /// <summary>多了這一次的最近執行</summary>
        public List<RecentItem> Recent { get; set; } = [];
    }

    /// <summary>結果那一塊的一列</summary>
    public class FactItem
    {
        /// <summary>這一列是什麼</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>值</summary>
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>InSpec 自己寫入之後：記下來的那幾列、等著歸檔的那幾筆與最近執行</summary>
    public class WrittenView
    {
        /// <summary>要寫回去的每一列執行，一筆一列，先後跟等著歸檔的那幾筆相同；接回的那一列有編號，其餘是新增</summary>
        public List<inspec> Rows { get; set; } = [];

        /// <summary>等著歸檔的每一筆；沒有寫入是零筆</summary>
        public List<PendingItem> Pending { get; set; } = [];

        /// <summary>多了它們的最近執行</summary>
        public List<RecentItem> Recent { get; set; } = [];
    }

    /// <summary>一筆等著歸檔的結果：InSpec 寫入的當下已經記成 inspec 的一列</summary>
    public class PendingItem
    {
        /// <summary>記在 inspec 的哪一列；新增的那一列寫進去之後才有編號，之前是 0</summary>
        public int Id { get; set; }

        /// <summary>InSpec 寫出的這一筆內容</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>料號：內容裡對得上的回答，沒有就是左邊欄位填的值</summary>
        public string Part { get; set; } = string.Empty;

        /// <summary>批號</summary>
        public string Lot { get; set; } = string.Empty;

        /// <summary>壓合</summary>
        public string Press { get; set; } = string.Empty;

        /// <summary>InSpec 寫入的時刻</summary>
        public DateTime Time { get; set; }

        /// <summary>寫入當下 InSpec 開著的量測程式名稱</summary>
        public string Program { get; set; } = string.Empty;

        /// <summary>那支量測程式在搜尋資料夾裡的哪一層；找不到是空字串</summary>
        public string ProgramFolder { get; set; } = string.Empty;
    }

    /// <summary>歸檔視窗按歸檔之後：寫不成的原因、要附加的檔與要寫回去的那一列</summary>
    public class ExportedView
    {
        /// <summary>寫不成的原因；寫成了是空字串</summary>
        public string Fault { get; set; } = string.Empty;

        /// <summary>要附加的歸檔檔；寫不成是零筆</summary>
        public List<ArchiveFile> Files { get; set; } = [];

        /// <summary>要寫回 inspec 的那一列；寫不成是零筆</summary>
        public List<inspec> Rows { get; set; } = [];
    }

    /// <summary>設定那一節</summary>
    public class SetupView
    {
        /// <summary>搜尋資料夾</summary>
        [StringLength(32767)]
        public string SearchFolder { get; set; } = string.Empty;

        /// <summary>系統紀錄資料夾</summary>
        [StringLength(32767)]
        public string LogFolder { get; set; } = string.Empty;

        /// <summary>備份輸出路徑</summary>
        [StringLength(32767)]
        public string BackupFolder { get; set; } = string.Empty;

        /// <summary>條碼解析間隔，毫秒；沒有值是 null</summary>
        public int? ScanInterval { get; set; }

        /// <summary>跑完顯示結果</summary>
        public bool ShowResult { get; set; }

        /// <summary>欄位結構的每一列</summary>
        public List<RowItem> Rows { get; set; } = [];
    }

    /// <summary>欄位結構的一列</summary>
    public class RowItem
    {
        /// <summary>inspec 那一列的編號；還沒存過是 0</summary>
        public int Id { get; set; }

        /// <summary>第幾欄，也是條碼的第幾段</summary>
        public int Order { get; set; }

        /// <summary>欄位名稱；長度照資料表欄位的定義</summary>
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        /// <summary>對應的 InSpec 提示文字；長度照資料表欄位的定義</summary>
        [StringLength(255)]
        public string Prompt { get; set; } = string.Empty;

        /// <summary>fill（直接填）／confirm（先確認再填）；新增的一列是直接填</summary>
        [StringLength(7)]
        public string Mode { get; set; } = "fill";

        /// <summary>主頁上可否改</summary>
        public bool Editable { get; set; }
    }

    /// <summary>存欄位結構之後：要寫回去的那幾列，與畫面上每一列對到的那一列</summary>
    public class FieldsSaved
    {
        /// <summary>要寫回 inspec 的每一列：新的、改了的、移掉的</summary>
        public List<inspec> Rows { get; set; } = [];

        /// <summary>存下來的每一個欄位，照順序</summary>
        public List<KeptField> Kept { get; set; } = [];
    }

    /// <summary>畫面上的一列與它存成的那一列</summary>
    public class KeptField
    {
        /// <summary>畫面上的那一列</summary>
        public RowItem Row { get; set; } = new();

        /// <summary>存成的那一列；新的寫進去之後就有編號</summary>
        public inspec Field { get; set; } = new();
    }

    #endregion
}

using HelixStudio.Pack;

// 產生一組簽套件的金鑰
var keys = args
    .Where(one => one == "key")
    .Select(_ => Packer.Key(args.ElementAtOrDefault(1) ?? "."))
    .ToList();

keys.ForEach(Console.WriteLine);

// 把一個資料夾打包成簽章過的套件
var packs = args
    .Where(one => one == "pack")
    .Where(_ => args.Length >= 4)
    .Select(_ => Packer.Pack(args[1], args[2], args[3]))
    .ToList();

packs.ForEach(Console.WriteLine);

// 產生一組發授權的金鑰
var seals = args
    .Where(one => one == "licence-key")
    .Select(_ => Licencer.Key(args.ElementAtOrDefault(1) ?? "."))
    .ToList();

seals.ForEach(Console.WriteLine);

// 看客戶送來的申請碼裡面是什麼
var reads = args
    .Where(one => one == "licence-read")
    .Where(_ => args.Length >= 2)
    .Select(_ => Licencer.Read(args[1]))
    .ToList();

reads.ForEach(Console.WriteLine);

// 照申請碼發一把授權
var issues = args
    .Where(one => one == "licence")
    .Where(_ => args.Length >= 4)
    .Select(_ => Licencer.Issue(args[1], args[2], args[3],
        args.ElementAtOrDefault(4) ?? string.Empty,
        args.ElementAtOrDefault(5) ?? string.Empty))
    .ToList();

issues.ForEach(Console.WriteLine);

// 一件都沒指名，或指名了卻少給值，就印出用法
var done = keys.Count + packs.Count + seals.Count + reads.Count + issues.Count;

Enumerable
    .Repeat($"{Packer.Usage}{Environment.NewLine}{Licencer.Usage}", 1)
    .Where(_ => done == 0)
    .ToList()
    .ForEach(Console.WriteLine);

return Math.Max(0, 1 - done);

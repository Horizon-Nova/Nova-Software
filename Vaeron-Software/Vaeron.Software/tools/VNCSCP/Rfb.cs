using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace VNCSCP;

/// <summary>跟機台上的 UltraVNC 講話：登入、建資料夾、一個一個送檔；不開檢視器、沒有視窗</summary>
/// <remarks>協定依據：SandBox\VNCSCP 的實測與舊版 VNCSCP 的 RfbHandshake、RfbFileTransferClient。出錯就擲出 IOException，訊息照實寫給人看。</remarks>
public static class Rfb
{
    /// <summary>連上機台並用 VNC 密碼登入，回傳登入好的連線與花了幾毫秒；沒登入成功就擲出原因</summary>
    /// <param name="ip">機台 IP</param>
    /// <param name="port">UltraVNC 的 Port</param>
    /// <param name="password">VNC 密碼</param>
    /// <param name="token">取消</param>
    public static async Task<Session> Login(string ip, int port, string password, CancellationToken token)
    {
        var started = DateTime.Now;
        var client = new TcpClient();

        try
        {
            // 連上：連不上就說連不上哪裡、等了多久
            using (var connect = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                connect.CancelAfter(Reach);
                try { await client.ConnectAsync(ip, port, connect.Token); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException($"連不上 {ip}:{port}：等了 {Reach.TotalSeconds:0} 秒沒有回應"); }
                catch (SocketException error) { throw new IOException($"連不上 {ip}:{port}：{error.Message}"); }
            }

            var stream = client.GetStream();

            // 版本橫幅：照伺服器的版本回一樣的
            var banner = await Read(stream, 12, token, $"{ip}:{port} 連上了，但沒有送出 VNC 的版本");
            await Write(stream, banner, token);
            var text = Encoding.ASCII.GetString(banner);
            var minor = int.TryParse(text.Length >= 11 ? text.Substring(8, 3) : "3", NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 3;

            // 驗證方式：3.7 以後是一串讓用戶端挑，3.3 是伺服器直接指定
            if (minor >= 7)
            {
                var count = (await Read(stream, 1, token, $"{ip}:{port} 沒有回驗證方式"))[0];
                if (count == 0) throw new IOException($"UltraVNC 拒絕連線 {ip}:{port}：{await Reason(stream, token)}");
                var offered = await Read(stream, count, token, $"{ip}:{port} 沒有回驗證方式");
                if (!offered.Contains(VncAuthentication)) throw new IOException($"{ip}:{port} 不接受 VNC 密碼登入（它給的驗證方式：{string.Join(",", offered)}）");
                await Write(stream, [VncAuthentication], token);
            }
            else
            {
                var type = Number(await Read(stream, 4, token, $"{ip}:{port} 沒有回驗證方式"));
                if (type == 0) throw new IOException($"UltraVNC 拒絕連線 {ip}:{port}：{await Reason(stream, token)}");
                if (type != VncAuthentication) throw new IOException($"{ip}:{port} 不接受 VNC 密碼登入（它要的驗證方式：{type}）");
            }

            // 挑戰與回應：密碼截成 8 個字元做 DES 金鑰
            var challenge = await Read(stream, 16, token, $"{ip}:{port} 沒有送出密碼挑戰");
            await Write(stream, Respond(challenge, password), token);

            var result = Number(await Read(stream, 4, token, $"{ip}:{port} 沒有回登入結果"));
            if (result != 0) throw new IOException($"UltraVNC 拒絕登入 {ip}:{port}：VNC 密碼不對");

            return new Session(client, stream, (int)(DateTime.Now - started).TotalMilliseconds);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>在登入好的連線上，把一批檔送到遠端資料夾；子資料夾先建，再一個一個送；每送一塊回報累計送了多少位元組</summary>
    /// <param name="session">登入好的連線</param>
    /// <param name="files">要送的檔：本機完整路徑與它在遠端資料夾底下的相對路徑</param>
    /// <param name="remote">遠端資料夾</param>
    /// <param name="sent">每送一塊就回報一次累計位元組</param>
    /// <param name="token">取消</param>
    public static async Task Send(Session session, IReadOnlyList<(string Local, string Relative)> files, string remote, Action<long> sent, CancellationToken token)
    {
        var stream = session.Stream;
        var root = remote.TrimEnd('\\', '/') + "\\";
        var total = 0L;

        // 開工作階段：ClientInit 共用畫面、讀 ServerInit、只要 Raw 編碼（不要求畫面，所以不會有畫面更新）
        await Write(stream, [1], token);
        var init = await Read(stream, 24, token, "機台沒有回工作階段");
        session.BytesPerPixel = Math.Max(1, init[4] / 8);
        await Skip(stream, Math.Min(Number(init.AsSpan(20, 4).ToArray()), 4096), token);
        await Write(stream, [2, 0, 0, 1, 0, 0, 0, 0], token);

        // 打開檔案傳輸：要一次遠端資料夾的內容，讀到結束為止
        await Write(stream, Compose(FileTransferRequest, 0, 0, null), token);
        await Write(stream, Compose(DirContentRequest, 1, 0, Path(root)), token);
        await Until(session, message => message.ContentType == DirPacket && message.Length == 0, token, $"機台沒有回 {root} 的內容");

        // 遠端資料夾本身與每一層子資料夾：從淺到深各建一次；已經有的伺服器回失敗，不影響
        var folders = new[] { root.TrimEnd('\\') }
            .Concat(files.Select(one => System.IO.Path.GetDirectoryName(root + one.Relative) ?? string.Empty))
            .Where(one => one.Length > 0)
            .SelectMany(one => Enumerable.Range(2, Math.Max(0, one.Split('\\').Length - 1)).Select(depth => string.Join("\\", one.Split('\\').Take(depth))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(one => one.Count(c => c == '\\'))
            .ToList();

        foreach (var folder in folders)
        {
            await Write(stream, Compose(Command, DirCreate, 0, Path(folder)), token);
            await Until(session, message => message.ContentType == CommandReturn, token, $"機台沒有回建資料夾 {folder} 的結果");
        }

        // 一個一個送：提出、等接受、分塊送、結束標記
        foreach (var (local, relative) in files)
        {
            var info = new FileInfo(local);
            var stamp = info.LastWriteTime.ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture);
            var offer = Compose(FileTransferOffer, 0, (uint)info.Length, Names.GetBytes($"{root + relative},{stamp}"))
                .Concat(Big((uint)(info.Length >> 32)))
                .ToArray();

            await Write(stream, offer, token);

            var reply = await Until(session, message => message.ContentType is FileAcceptHeader or FileTransferRequest, token, $"機台沒有回要不要收 {relative}");
            if (reply.ContentType != FileAcceptHeader) throw new IOException($"機台不收 {relative}");
            if (reply.Size == CannotCreate) throw new IOException($"機台建不出 {root + relative}：確認這個資料夾存在而且可以寫入");

            await using var file = new FileStream(local, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, Block, true);
            var buffer = new byte[Block];
            int count;

            while ((count = await file.ReadAsync(buffer, token)) > 0)
            {
                await Write(stream, Compose(FilePacket, 0, 0, buffer.AsSpan(0, count).ToArray()), token);
                total += count;
                sent(total);
            }

            await Write(stream, Compose(EndOfFile, 0, 0, null), token);
        }
    }

    /// <summary>登入好的一條連線；用完就關</summary>
    public sealed class Session(TcpClient client, NetworkStream stream, int connectedMs) : IDisposable
    {
        /// <summary>這條連線的資料流</summary>
        public NetworkStream Stream { get; } = stream;

        /// <summary>從開始連線到登入成功花了幾毫秒</summary>
        public int ConnectedMs { get; } = connectedMs;

        /// <summary>每像素幾位元組：略過畫面更新時要用</summary>
        public int BytesPerPixel { get; set; } = 4;

        /// <summary>關掉連線</summary>
        public void Dispose() => client.Dispose();
    }

    #region 協定

    /// <summary>讀到要的那一則檔案傳輸訊息為止；中間的其他訊息照長度讀掉</summary>
    private static async Task<Message> Until(Session session, Func<Message, bool> wanted, CancellationToken token, string silent)
    {
        while (true)
        {
            var message = await Next(session, token, silent);
            if (message.ContentType == AbortFileTransfer) throw new IOException($"機台中止了傳輸：{Names.GetString(message.Data).TrimEnd('\0')}");
            if (wanted(message)) return message;
        }
    }

    /// <summary>讀下一則檔案傳輸訊息；畫面更新、鈴聲、剪貼簿照長度略過</summary>
    private static async Task<Message> Next(Session session, CancellationToken token, string silent)
    {
        var stream = session.Stream;

        while (true)
        {
            var type = (await Read(stream, 1, token, silent))[0];

            if (type == FileTransfer)
            {
                var head = await Read(stream, 11, token, silent);
                var size = Number(head.AsSpan(3, 4).ToArray());
                var length = Number(head.AsSpan(7, 4).ToArray());
                var data = length <= MaxKept ? await Read(stream, (int)length, token, silent) : [];
                if (length > MaxKept) await Skip(stream, length, token);
                return new Message(head[0], head[1], size, length, data);
            }

            if (type == FramebufferUpdate)
            {
                var rectangles = Number([0, 0, .. (await Read(stream, 3, token, silent)).AsSpan(1, 2)]);
                for (var i = 0; i < rectangles; i++)
                {
                    var rectangle = await Read(stream, 12, token, silent);
                    var width = Number([0, 0, rectangle[4], rectangle[5]]);
                    var height = Number([0, 0, rectangle[6], rectangle[7]]);
                    var encoding = Number(rectangle.AsSpan(8, 4).ToArray());
                    if (encoding == 0) await Skip(stream, (long)width * height * session.BytesPerPixel, token);
                    else if (encoding == 1) await Skip(stream, 4, token);
                    else throw new IOException($"機台送來看不懂的畫面編碼 {encoding}");
                }
                continue;
            }

            if (type == Bell) continue;

            if (type == CutText)
            {
                await Read(stream, 3, token, silent);
                await Skip(stream, Number(await Read(stream, 4, token, silent)), token);
                continue;
            }

            throw new IOException($"機台送來看不懂的訊息 {type}");
        }
    }

    /// <summary>組一則檔案傳輸訊息：type、contentType、contentParam、pad、size、length、資料</summary>
    private static byte[] Compose(byte contentType, byte contentParam, uint size, byte[]? data)
    {
        var body = data ?? [];
        return [FileTransfer, contentType, contentParam, 0, .. Big(size), .. Big((uint)body.Length), .. body];
    }

    /// <summary>遠端路徑：UTF-8、結尾補 0</summary>
    private static byte[] Path(string path) => Names.GetBytes(path + "\0");

    /// <summary>VNC 驗證的回應：密碼截斷補零到 8 bytes，每個 byte 位元反轉當 DES 金鑰，加密 16 bytes 挑戰</summary>
    private static byte[] Respond(byte[] challenge, string password)
    {
        var key = Encoding.ASCII.GetBytes(password).Concat(new byte[8]).Take(8)
            .Select(one => (byte)Enumerable.Range(0, 8).Aggregate(0, (bits, at) => (bits << 1) | ((one >> at) & 1)))
            .ToArray();

#pragma warning disable SYSLIB0021
        using var des = DES.Create();
#pragma warning restore SYSLIB0021
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.None;
        des.Key = key;

        using var encryptor = des.CreateEncryptor();
        var response = new byte[16];
        encryptor.TransformBlock(challenge, 0, 16, response, 0);
        return response;
    }

    /// <summary>伺服器拒絕時附的原因</summary>
    private static async Task<string> Reason(NetworkStream stream, CancellationToken token)
    {
        try
        {
            var length = Number(await Read(stream, 4, token, "沒有原因"));
            return Encoding.UTF8.GetString(await Read(stream, (int)Math.Min(length, 1024), token, "沒有原因"));
        }
        catch (IOException) { return "沒有附原因（多半是試錯太多次被暫時鎖住）"; }
    }

    /// <summary>讀滿 count 個位元組；等太久或對方關掉就擲出給人看的原因</summary>
    private static async Task<byte[]> Read(NetworkStream stream, int count, CancellationToken token, string silent)
    {
        var buffer = new byte[count];
        using var step = Step(token);

        try { await Fill(stream, buffer, step.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException($"{silent}：等了 {Wait.TotalSeconds:0} 秒"); }
        catch (EndOfStreamException) { throw new IOException($"{silent}：機台把連線關了"); }

        return buffer;
    }

    /// <summary>照長度讀掉不要的內容</summary>
    private static async Task Skip(NetworkStream stream, long count, CancellationToken token)
    {
        var buffer = new byte[65536];
        for (var left = count; left > 0;)
        {
            var take = (int)Math.Min(left, buffer.Length);
            using var step = Step(token);
            await Fill(stream, buffer.AsMemory(0, take), step.Token);
            left -= take;
        }
    }

    /// <summary>讀滿整段；對方關掉就擲出 EndOfStreamException（.NET 6 沒有 ReadExactlyAsync）</summary>
    private static async Task Fill(NetworkStream stream, Memory<byte> buffer, CancellationToken token)
    {
        for (var read = 0; read < buffer.Length;)
        {
            var got = await stream.ReadAsync(buffer[read..], token);
            if (got == 0) throw new EndOfStreamException();
            read += got;
        }
    }

    private static async Task Write(NetworkStream stream, byte[] bytes, CancellationToken token)
    {
        using var step = Step(token);
        await stream.WriteAsync(bytes, step.Token);
        await stream.FlushAsync(step.Token);
    }

    /// <summary>每一步各自的逾時，也跟著整體的取消</summary>
    private static CancellationTokenSource Step(CancellationToken token)
    {
        var step = CancellationTokenSource.CreateLinkedTokenSource(token);
        step.CancelAfter(Wait);
        return step;
    }

    private static uint Number(byte[] bytes) => bytes.Aggregate(0u, (sum, one) => (sum << 8) | one);

    private static byte[] Big(uint value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    /// <summary>一則檔案傳輸訊息</summary>
    private sealed record Message(byte ContentType, byte ContentParam, uint Size, uint Length, byte[] Data);

    #endregion

    #region 對照

    /// <summary>每一步最多等多久</summary>
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    /// <summary>連上機台最多等多久</summary>
    private static readonly TimeSpan Reach = TimeSpan.FromSeconds(3);

    /// <summary>遠端路徑與檔名用 UTF-8：2026-10-15 對 UltraVNC 1.8.3 實測，用 ANSI（Big5）送的中文檔名在機台上變亂碼，UTF-8 才對</summary>
    private static readonly Encoding Names = new UTF8Encoding(false);

    /// <summary>一塊送多少</summary>
    private const int Block = 8192;

    /// <summary>收到的訊息內容超過這麼長就讀掉不留</summary>
    private const uint MaxKept = 1 << 20;

    private const byte VncAuthentication = 2;
    private const byte FramebufferUpdate = 0;
    private const byte Bell = 2;
    private const byte CutText = 3;
    private const byte FileTransfer = 7;
    private const byte DirContentRequest = 1;
    private const byte DirPacket = 2;
    private const byte FileTransferRequest = 3;
    private const byte FilePacket = 5;
    private const byte EndOfFile = 6;
    private const byte AbortFileTransfer = 7;
    private const byte FileTransferOffer = 8;
    private const byte FileAcceptHeader = 9;
    private const byte Command = 10;
    private const byte CommandReturn = 11;
    private const byte DirCreate = 1;
    private const uint CannotCreate = 0xFFFFFFFF;

    #endregion
}

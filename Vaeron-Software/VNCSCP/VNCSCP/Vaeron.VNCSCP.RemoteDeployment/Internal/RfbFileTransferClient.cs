using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using Vaeron.Kernel;
using Vaeron.Kernel.Guarding;
using Vaeron.Kernel.Secrets;
using Vaeron.VNCSCP.Internal.Rfb;

namespace Vaeron.VNCSCP.RemoteDeployment.Internal;

/// <summary>一次傳輸的目標與內容。</summary>
internal sealed record RfbTransferRequest(
    string Ip, int Port, string LocalSourcePath, string RemoteTargetPath, TimeSpan OperationTimeout);

/// <summary>傳輸結果：成敗、給人看的訊息、逐步診斷。</summary>
internal sealed record RfbTransferOutcome(bool Success, string Message, string Diagnostic);

/// <summary>一則 FileTransfer 訊息。</summary>
internal sealed record FtMessage(byte ContentType, uint Size, uint Length, byte[] Data);

/// <summary>要送出去的內容與它在遠端的完整路徑。</summary>
internal sealed record TransferPayload(byte[] Bytes, string RemoteName, string? TempZipPath);

/// <summary>RFB File Transfer 純協定傳輸：不開檢視器、無視窗，直接把檔案或資料夾送到遠端。</summary>
internal class RfbFileTransferClient
{
    private const int BlockSize = 8192;
    private const byte FileTransferMessage = 7;
    private const uint CannotCreateFile = 0xFFFFFFFF;

    /// <summary>遠端路徑與檔名的編碼採伺服器 ANSI 碼頁，非 UTF-8。</summary>
    private static readonly Encoding PathEncoding;

    static RfbFileTransferClient()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        PathEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
    }

    internal virtual async Task<RfbTransferOutcome> SendAsync(
        RfbTransferRequest request, SecretBuffer password, CancellationToken cancellationToken)
    {
        var trace = new RfbTrace();

        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operation.CancelAfter(request.OperationTimeout);
        var token = operation.Token;

        var prepared = await PreparePayloadAsync(request, token);
        using var cleanup = new ScopeGuard(() => DeleteTempFiles(prepared));

        var sent = await prepared.BindAsync(payload => SendPayloadAsync(request, payload, password, trace, token));

        return sent.Values
            .Select(message => new RfbTransferOutcome(true, message, trace.Text))
            .DefaultIfEmpty(new RfbTransferOutcome(false, sent.Message, trace.Text))
            .First();
    }

    private async ValueTask<Result<string>> SendPayloadAsync(
        RfbTransferRequest request, TransferPayload payload, SecretBuffer password, RfbTrace trace, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(request.Ip, request.Port, cancellationToken);
        await using var stream = client.GetStream();

        var authenticated = await RfbHandshake.AuthenticateAsync(stream, password, trace, cancellationToken);

        var admitted = authenticated.Bind(result => Result<uint>.When(
            result.SecurityResultCode == 0, result.SecurityResultCode, ResultKind.ValidationFailed, "密碼錯誤或連線被拒"));

        var opened = await admitted.BindAsync(_ => InitializeSessionAsync(stream, trace, cancellationToken));

        return await opened.BindAsync(bytesPerPixel =>
            TransferAsync(stream, bytesPerPixel, request, payload, trace, cancellationToken));
    }

    /// <summary>ClientInit 與 ServerInit，取得每像素位元組數供之後略過畫面更新封包使用。</summary>
    private static async ValueTask<Result<int>> InitializeSessionAsync(
        NetworkStream stream, RfbTrace trace, CancellationToken cancellationToken)
    {
        await RfbStream.WriteAsync(stream, [1], cancellationToken);

        var serverInit = await RfbStream.ReadExactAsync(stream, 24, cancellationToken);
        var bitsPerPixel = serverInit.Skip(4).Take(1).Sum(value => (int)value);
        var bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
        var nameLength = RfbStream.BigEndian(serverInit.Skip(20).Take(4).ToArray());

        await RfbStream.SkipAsync(stream, Math.Min(nameLength, 4096), cancellationToken);
        await RfbStream.WriteAsync(stream, [2, 0, 0, 1, 0, 0, 0, 0], cancellationToken);

        trace.Write($"已建立工作階段，每像素 {bytesPerPixel} bytes");
        return Result<int>.Ok(bytesPerPixel);
    }

    private static async ValueTask<Result<string>> TransferAsync(
        NetworkStream stream, int bytesPerPixel, RfbTransferRequest request, TransferPayload payload,
        RfbTrace trace, CancellationToken cancellationToken)
    {
        var remoteDirectory = request.RemoteTargetPath.TrimEnd('\\', '/') + "\\";

        await RfbStream.WriteAsync(stream, Compose(3, 0, 0, null), cancellationToken);
        await RfbStream.WriteAsync(stream, Compose(1, 1, 0, PathEncoding.GetBytes(remoteDirectory + "\0")), cancellationToken);

        var listed = await DrainDirectoryAsync(stream, bytesPerPixel, cancellationToken);
        trace.Write($"已設定遠端目錄：{remoteDirectory}");

        var offered = await listed.BindAsync(_ => OfferAsync(stream, bytesPerPixel, payload, cancellationToken));

        return await offered.BindAsync(async _ =>
        {
            await UploadBlocksAsync(stream, payload.Bytes, cancellationToken);
            trace.Write($"上傳完成：{payload.RemoteName}");
            return Result<string>.Ok("部署成功");
        });
    }

    /// <summary>送出檔案提議並等待伺服器接受。</summary>
    private static async ValueTask<Result<uint>> OfferAsync(
        NetworkStream stream, int bytesPerPixel, TransferPayload payload, CancellationToken cancellationToken)
    {
        var stamp = DateTimeOffset.Now.ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture);
        var descriptor = PathEncoding.GetBytes($"{payload.RemoteName},{stamp}");
        var offer = Compose(8, 0, (uint)payload.Bytes.Length, descriptor).Concat(new byte[4]).ToArray();

        await RfbStream.WriteAsync(stream, offer, cancellationToken);

        var replies = new List<FtMessage>();
        var reads = 0;

        while (replies.Count == 0 && reads < 32)
        {
            var message = await ReadFtAsync(stream, bytesPerPixel, cancellationToken);
            replies.AddRange(new[] { message }.Where(reply => reply.ContentType is 9 or 3));
            reads++;
        }

        return Result<FtMessage>
            .FromFirst(replies, ResultKind.Unavailable, "等不到伺服器接受回應")
            .Bind(reply => Result<FtMessage>.When(
                reply.ContentType == 9, reply, ResultKind.Conflict, "遠端拒絕檔案傳輸"))
            .Bind(reply => Result<uint>.When(
                reply.Size != CannotCreateFile, reply.Size, ResultKind.Unavailable,
                "遠端無法建立目的檔，請確認目標目錄存在且可寫入"));
    }

    private static async Task UploadBlocksAsync(NetworkStream stream, byte[] bytes, CancellationToken cancellationToken)
    {
        var blockCount = (bytes.Length + BlockSize - 1) / BlockSize;

        foreach (var index in Enumerable.Range(0, blockCount))
        {
            var offset = index * BlockSize;
            var take = Math.Min(BlockSize, bytes.Length - offset);
            var chunk = bytes.AsSpan(offset, take).ToArray();
            await RfbStream.WriteAsync(stream, Compose(5, 0, 0, chunk), cancellationToken);
        }

        await RfbStream.WriteAsync(stream, Compose(6, 0, 0, null), cancellationToken);
    }

    /// <summary>讀完目錄列表直到結束封包。</summary>
    private static async ValueTask<Result<int>> DrainDirectoryAsync(
        NetworkStream stream, int bytesPerPixel, CancellationToken cancellationToken)
    {
        var terminators = new List<FtMessage>();
        var reads = 0;

        while (terminators.Count == 0 && reads < 512)
        {
            var message = await ReadFtAsync(stream, bytesPerPixel, cancellationToken);
            terminators.AddRange(new[] { message }.Where(item => item.ContentType == 2 && item.Length == 0));
            reads++;
        }

        return Result<int>.When(terminators.Count > 0, reads, ResultKind.Unavailable, "等不到伺服器目錄列表回應");
    }

    private static async ValueTask<Result<TransferPayload>> PreparePayloadAsync(
        RfbTransferRequest request, CancellationToken cancellationToken)
    {
        var remoteDirectory = request.RemoteTargetPath.TrimEnd('\\', '/') + "\\";

        var candidates = new[] { request.LocalSourcePath }
            .Where(File.Exists)
            .Select(path => FromFileAsync(path, remoteDirectory, cancellationToken))
            .Concat(new[] { request.LocalSourcePath }
                .Where(Directory.Exists)
                .Select(path => FromDirectoryAsync(path, remoteDirectory, cancellationToken)));

        var prepared = await Task.WhenAll(candidates);
        return Result<TransferPayload>.FromFirst(prepared, ResultKind.ValidationFailed, "來源路徑不存在");
    }

    private static async Task<TransferPayload> FromFileAsync(
        string path, string remoteDirectory, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return new TransferPayload(bytes, remoteDirectory + Path.GetFileName(path), null);
    }

    /// <summary>資料夾打包成 zip 送出。內部項目名用伺服器 ANSI 碼頁，UTF-8 會讓遠端解不出中文檔名。</summary>
    private static async Task<TransferPayload> FromDirectoryAsync(
        string path, string remoteDirectory, CancellationToken cancellationToken)
    {
        var folderName = Path.GetFileName(path.TrimEnd('\\', '/'));
        var tempZip = Path.Combine(Path.GetTempPath(), $"vaeron_ft_{Guid.NewGuid():N}.zip");

        ZipFile.CreateFromDirectory(path, tempZip, CompressionLevel.Optimal, includeBaseDirectory: false, PathEncoding);

        var bytes = await File.ReadAllBytesAsync(tempZip, cancellationToken);
        return new TransferPayload(bytes, $"{remoteDirectory}!UVNCDIR-{folderName}.zip", tempZip);
    }

    private static void DeleteTempFiles(Result<TransferPayload> prepared)
    {
        var temporaries = prepared.Values.Select(payload => payload.TempZipPath).OfType<string>();

        foreach (var tempZip in temporaries)
        {
            Guard.Run(() => File.Delete(tempZip), _ => { });
        }
    }

    /// <summary>組 rfbFileTransfer 訊息：type、contentType、contentParam、pad、size、length、資料。</summary>
    private static byte[] Compose(byte contentType, byte contentParam, uint size, byte[]? data)
    {
        var payload = data ?? [];
        var head = new byte[] { FileTransferMessage, contentType, contentParam, 0 };

        return head
            .Concat(RfbStream.ToBigEndian(size))
            .Concat(RfbStream.ToBigEndian((uint)payload.Length))
            .Concat(payload)
            .ToArray();
    }

    private static async Task<FtMessage> ReadFtAsync(NetworkStream stream, int bytesPerPixel, CancellationToken cancellationToken)
    {
        var header = await ReadFtHeaderAsync(stream, bytesPerPixel, cancellationToken);
        var contentType = header.Skip(1).Take(1).Sum(value => (int)value);
        var size = RfbStream.BigEndian(header.Skip(4).Take(4).ToArray());
        var length = RfbStream.BigEndian(header.Skip(8).Take(4).ToArray());

        var reads = Enumerable.Repeat(length, 1)
            .Where(value => value is > 0 and < 5_000_000)
            .Select(value => RfbStream.ReadExactAsync(stream, (int)value, cancellationToken));

        var data = await reads
            .DefaultIfEmpty(Task.FromResult(Array.Empty<byte>()))
            .First();

        return new FtMessage((byte)contentType, size, length, data);
    }

    /// <summary>略過畫面更新、鈴聲、剪貼簿訊息，回傳 FileTransfer 訊息的標頭。</summary>
    private static async Task<byte[]> ReadFtHeaderAsync(NetworkStream stream, int bytesPerPixel, CancellationToken cancellationToken)
    {
        var header = Array.Empty<byte>();

        while (header.Length == 0)
        {
            var announced = await RfbStream.ReadExactAsync(stream, 1, cancellationToken);
            var type = announced.Take(1).Sum(value => (int)value);
            var handle = SkipperByMessageType.GetValueOrDefault((byte)type, RejectUnknownMessageAsync);
            header = await handle(stream, bytesPerPixel, cancellationToken);
        }

        return header;
    }

    /// <summary>伺服器可能送來的訊息型別，逐型別對應一種處置。</summary>
    private static readonly Dictionary<byte, Func<NetworkStream, int, CancellationToken, Task<byte[]>>> SkipperByMessageType = new()
    {
        [FileTransferMessage] = ReadFileTransferHeaderAsync,
        [0] = SkipFramebufferUpdateAsync,
        [2] = SkipBellAsync,
        [3] = SkipCutTextAsync
    };

    private static async Task<byte[]> ReadFileTransferHeaderAsync(
        NetworkStream stream, int bytesPerPixel, CancellationToken cancellationToken)
    {
        var rest = await RfbStream.ReadExactAsync(stream, 11, cancellationToken);
        var head = new byte[] { FileTransferMessage };
        return head.Concat(rest).ToArray();
    }

    private static async Task<byte[]> SkipFramebufferUpdateAsync(
        NetworkStream stream, int bytesPerPixel, CancellationToken cancellationToken)
    {
        await RfbStream.ReadExactAsync(stream, 1, cancellationToken);
        var counts = await RfbStream.ReadExactAsync(stream, 2, cancellationToken);
        var rectangles = (int)RfbStream.BigEndian(new byte[] { 0, 0 }.Concat(counts).ToArray());

        foreach (var _ in Enumerable.Range(0, rectangles))
        {
            await SkipRectangleAsync(stream, bytesPerPixel, cancellationToken);
        }

        return [];
    }

    private static async Task SkipRectangleAsync(NetworkStream stream, int bytesPerPixel, CancellationToken cancellationToken)
    {
        var rectangle = await RfbStream.ReadExactAsync(stream, 12, cancellationToken);
        var width = (int)RfbStream.BigEndian(new byte[] { 0, 0 }.Concat(rectangle.Skip(4).Take(2)).ToArray());
        var height = (int)RfbStream.BigEndian(new byte[] { 0, 0 }.Concat(rectangle.Skip(6).Take(2)).ToArray());
        var encoding = RfbStream.BigEndian(rectangle.Skip(8).Take(4).ToArray());

        var skip = SkipperByEncoding.GetValueOrDefault(encoding, RejectUnknownEncodingAsync);
        await skip(stream, (long)width * height * bytesPerPixel, cancellationToken);
    }

    /// <summary>畫面更新的編碼方式，逐種對應要略過的長度。</summary>
    private static readonly Dictionary<uint, Func<NetworkStream, long, CancellationToken, Task>> SkipperByEncoding = new()
    {
        [0] = SkipRawAsync,
        [1] = SkipCopyRectAsync
    };

    private static Task SkipRawAsync(NetworkStream stream, long length, CancellationToken cancellationToken)
        => RfbStream.SkipAsync(stream, length, cancellationToken);

    private static Task SkipCopyRectAsync(NetworkStream stream, long length, CancellationToken cancellationToken)
        => RfbStream.SkipAsync(stream, 4, cancellationToken);

    private static Task RejectUnknownEncodingAsync(NetworkStream stream, long length, CancellationToken cancellationToken)
        => throw new IOException("未支援的畫面更新編碼");

    private static Task<byte[]> SkipBellAsync(NetworkStream stream, int bytesPerPixel, CancellationToken cancellationToken)
        => Task.FromResult(Array.Empty<byte>());

    private static async Task<byte[]> SkipCutTextAsync(NetworkStream stream, int bytesPerPixel, CancellationToken cancellationToken)
    {
        await RfbStream.ReadExactAsync(stream, 3, cancellationToken);
        var length = RfbStream.BigEndian(await RfbStream.ReadExactAsync(stream, 4, cancellationToken));
        await RfbStream.SkipAsync(stream, length, cancellationToken);
        return [];
    }

    private static Task<byte[]> RejectUnknownMessageAsync(NetworkStream stream, int bytesPerPixel, CancellationToken cancellationToken)
        => throw new IOException("未知的 RFB 訊息型別");
}

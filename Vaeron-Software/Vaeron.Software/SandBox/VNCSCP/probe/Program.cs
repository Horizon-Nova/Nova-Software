// 用反射叫出 VNCSCP 既有的 RfbFileTransferClient，對真的 UltraVNC 伺服器送一個檔案或一個資料夾，印出成敗與逐步診斷。
// 用法：VncscpProbe <ip> <port> <本機來源路徑> <遠端目的目錄> <密碼>
using System.Reflection;
using System.Text;
using Vaeron.Kernel.Secrets;

var ip = args[0];
var port = int.Parse(args[1]);
var source = args[2];
var remoteDir = args[3];
var password = args[4];

// 載入 VNCSCP 建好的傳輸組件，取出內部的傳輸類別與請求型別
var asm = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Vaeron.VNCSCP.RemoteDeployment.dll"));
var clientType = asm.GetType("Vaeron.VNCSCP.RemoteDeployment.Internal.RfbFileTransferClient")!;
var requestType = asm.GetType("Vaeron.VNCSCP.RemoteDeployment.Internal.RfbTransferRequest")!;

// 組一次傳輸請求：目標、來源、遠端目錄、逾時
var request = Activator.CreateInstance(
    requestType, ip, port, source, remoteDir, TimeSpan.FromMinutes(10))!;

// 密碼用 VNC Authentication 的原始位元組；交給既有程式自己截斷補零
using var secret = new SecretBuffer(Encoding.ASCII.GetBytes(password));

// 建立傳輸器並呼叫它唯一的對外方法
var client = Activator.CreateInstance(clientType, nonPublic: true)!;
var send = clientType.GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
var task = (Task)send.Invoke(client, new object[] { request, secret, CancellationToken.None })!;
await task;

// 讀回結果：成敗、訊息、診斷
var outcome = task.GetType().GetProperty("Result")!.GetValue(task)!;
var ok = (bool)outcome.GetType().GetProperty("Success")!.GetValue(outcome)!;
var message = (string)outcome.GetType().GetProperty("Message")!.GetValue(outcome)!;
var diagnostic = (string)outcome.GetType().GetProperty("Diagnostic")!.GetValue(outcome)!;

Console.WriteLine($"SUCCESS={ok}");
Console.WriteLine($"MESSAGE={message}");
Console.WriteLine("---- DIAGNOSTIC ----");
Console.WriteLine(diagnostic);
Environment.Exit(ok ? 0 : 1);

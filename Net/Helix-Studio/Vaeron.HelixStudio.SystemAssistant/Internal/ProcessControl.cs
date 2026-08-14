using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Vaeron.HelixStudio.SystemAssistant.Internal;

/// <summary>對 Windows 下達的動作。動作名稱的值域來自 toggle_action，新增一種動作多一列。</summary>
/// <remarks>
/// 結束走 TerminateProcess 而非 Process.Kill：拒絕存取時前者以回傳值表達，後者擲例外。
/// 啟動走 ShellExecuteEx，同樣以回傳值表達成敗。
/// </remarks>
internal sealed class ProcessControl
{
    private const int Terminate = 0x0001;
    private const int ShowNormal = 1;
    private const int FlagNoUi = 0x00000400;

    private static readonly Dictionary<string, Action<ToggleTarget>> Actions = new()
    {
        ["terminate"] = static target => Process.GetProcessesByName(target.SoftwareId)
            .ToList()
            .ForEach(End),
        ["launch"] = static target => Launch(target.ExePath)
    };

    /// <summary>查不到動作名稱者不執行任何東西。不可按的組合在查詢時就沒有列。</summary>
    public void Run(ToggleTarget target) =>
        Actions
            .Where(pair => pair.Key == target.Action)
            .Select(pair => pair.Value)
            .ToList()
            .ForEach(action => action(target));

    private static void End(Process process)
    {
        using var handle = OpenProcess(Terminate, false, process.Id);
        TerminateProcess(handle, 1);
    }

    private static void Launch(string path)
    {
        var info = new ShellExecuteInfo
        {
            Size = Marshal.SizeOf<ShellExecuteInfo>(),
            Mask = FlagNoUi,
            Verb = "open",
            File = path,
            Show = ShowNormal
        };

        ShellExecuteEx(ref info);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(int access, bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ShellExecuteEx(ref ShellExecuteInfo info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellExecuteInfo
    {
        public int Size;
        public int Mask;
        public IntPtr Window;
        [MarshalAs(UnmanagedType.LPWStr)] public string Verb;
        [MarshalAs(UnmanagedType.LPWStr)] public string File;
        [MarshalAs(UnmanagedType.LPWStr)] public string Parameters;
        [MarshalAs(UnmanagedType.LPWStr)] public string Directory;
        public int Show;
        public IntPtr Instance;
        public IntPtr IdList;
        [MarshalAs(UnmanagedType.LPWStr)] public string Class;
        public IntPtr KeyClass;
        public uint HotKey;
        public IntPtr Icon;
        public IntPtr Process;
    }
}

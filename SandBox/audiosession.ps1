# 哪個軟體正在用哪個音訊裝置（含 USB 耳機與麥克風）。
# COM 介面配置與 C# 端一對一對應。
#
# IAudioSessionControl2 繼承 IAudioSessionControl 的九個方法，其後才是自己的五個。
# 前面少算一格會使 GetProcessId 回垃圾值而不報錯。

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] public class MMDeviceEnumerator { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceEnumerator {
  int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
  int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceCollection { int GetCount(out int count); int Item(int index, out IMMDevice device); }

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDevice {
  int Activate(ref Guid iid, int ctx, IntPtr param, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
  int OpenPropertyStore(int access, out IPropertyStore store);
  int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
  int GetState(out int state);
}

[ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPropertyStore {
  int GetCount(out int c); int GetAt(int i, out PROPERTYKEY k);
  int GetValue(ref PROPERTYKEY k, out PROPVARIANT v); int SetValue(ref PROPERTYKEY k, ref PROPVARIANT v); int Commit();
}

[StructLayout(LayoutKind.Sequential)] public struct PROPERTYKEY { public Guid fmtid; public int pid; }

[StructLayout(LayoutKind.Explicit)] public struct PROPVARIANT {
  [FieldOffset(0)] public short vt; [FieldOffset(8)] public IntPtr p;
  public string Text { get { return vt == 31 ? Marshal.PtrToStringUni(p) : null; } } }

[ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionManager2 {
  int GetAudioSessionControl(IntPtr guid, int flags, out IntPtr ctl);
  int GetSimpleAudioVolume(IntPtr guid, int flags, out IntPtr vol);
  int GetSessionEnumerator(out IAudioSessionEnumerator e);
}

[ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionEnumerator { int GetCount(out int count); int GetSession(int i, out IAudioSessionControl2 s); }

[ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionControl2 {
  int GetState(out int state);
  int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string n);
  int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string n, IntPtr c);
  int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string p);
  int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string p, IntPtr c);
  int GetGroupingParam(out Guid g);
  int SetGroupingParam(IntPtr g, IntPtr c);
  int RegisterAudioSessionNotification(IntPtr n);
  int UnregisterAudioSessionNotification(IntPtr n);
  int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
  int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
  int GetProcessId(out int pid);
  int IsSystemSoundsSession();
  int SetDuckingPreference(bool opt);
}

public static class Audio {
  static string[] StateName = { "非使用中", "使用中", "已到期" };
  static string[] FlowName = { "輸出", "輸入" };

  public static void Dump() {
    var e = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
    for (int flow = 0; flow < 2; flow++) {
      IMMDeviceCollection col; e.EnumAudioEndpoints(flow, 1, out col);
      int n; col.GetCount(out n);
      for (int i = 0; i < n; i++) {
        IMMDevice dev; col.Item(i, out dev);
        string id; dev.GetId(out id);
        IPropertyStore store; dev.OpenPropertyStore(0, out store);
        var key = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
        PROPVARIANT pv; store.GetValue(ref key, out pv);
        Console.WriteLine("[" + FlowName[flow] + "] " + pv.Text);
        Console.WriteLine("        " + id);

        var iid = typeof(IAudioSessionManager2).GUID; object mgr;
        if (dev.Activate(ref iid, 1, IntPtr.Zero, out mgr) != 0) continue;
        IAudioSessionEnumerator se; ((IAudioSessionManager2)mgr).GetSessionEnumerator(out se);
        int sn; se.GetCount(out sn);
        for (int s = 0; s < sn; s++) {
          IAudioSessionControl2 ctl; se.GetSession(s, out ctl);
          int pid; ctl.GetProcessId(out pid);
          int st; ctl.GetState(out st);
          string proc = "系統音效";
          try { proc = System.Diagnostics.Process.GetProcessById(pid).ProcessName; } catch { }
          string label = (st >= 0 && st < 3) ? StateName[st] : ("未知狀態 " + st);
          Console.WriteLine("           pid=" + pid + " (" + proc + ")  " + label);
        }
      }
    }
  }
}
'@ -ErrorAction Stop

[Audio]::Dump()

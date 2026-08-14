using System.Runtime.InteropServices;

namespace Vaeron.Kernel.Handles;

/// <summary>非受管 handle 的基底。SafeHandle 提供關鍵完成項,防止 handle 被提早回收或被作業系統回收後再利用。有它即不需自寫 finalizer。</summary>
public abstract class SafeHandleBase : SafeHandle
{
    protected SafeHandleBase(bool ownsHandle)
        : base(IntPtr.Zero, ownsHandle)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;
}

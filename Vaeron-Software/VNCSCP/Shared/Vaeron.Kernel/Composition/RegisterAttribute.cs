namespace Vaeron.Kernel.Composition;

public enum Lifetime
{
    Singleton,
    Transient,
    Scoped
}

/// <summary>標記此型別由產生器自動註冊。產生器透過語意模型於編譯期讀取,生成的程式碼不引用本屬性。</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RegisterAttribute : Attribute
{
    public RegisterAttribute(Lifetime lifetime = Lifetime.Singleton) => Lifetime = lifetime;

    public Lifetime Lifetime { get; }
}

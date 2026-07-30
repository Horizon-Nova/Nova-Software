namespace Vaeron.Kernel.Identity;

/// <summary>
/// Guid 識別碼的強型別包裝。
///
/// TEntity 只作型別標記,不要求任何實際關聯——用途是讓兩個底層同為 Guid 的識別碼
/// 在編譯期無法互相誤用(把 UserId 傳進要 OrderId 的參數,編譯器擋得住)。
///
/// 轉換一律 explicit。若開放 implicit,任何裸 Guid 都能無聲通過型別檢查,
/// 本型別要解決的問題就消失,等於沒加。
///
/// 不依賴任何套件：IParsable / IComparable 皆為 BCL 原生介面。
/// MVC 路由繫結會自動使用 IParsable,WPF 不使用它也能編譯——
/// 本型別對宿主框架無假設。
/// </summary>
public readonly record struct StronglyTypedId<TEntity>(Guid Value)
    : IComparable<StronglyTypedId<TEntity>>, IParsable<StronglyTypedId<TEntity>>
{
    public static StronglyTypedId<TEntity> New() => new(Guid.NewGuid());

    public int CompareTo(StronglyTypedId<TEntity> other) => Value.CompareTo(other.Value);

    public static StronglyTypedId<TEntity> Parse(string s, IFormatProvider? provider)
        => new(Guid.Parse(s, provider));

    public static bool TryParse(
        string? s,
        IFormatProvider? provider,
        out StronglyTypedId<TEntity> result)
    {
        if (Guid.TryParse(s, provider, out var value))
        {
            result = new StronglyTypedId<TEntity>(value);
            return true;
        }

        result = default;
        return false;
    }

    public static explicit operator Guid(StronglyTypedId<TEntity> id) => id.Value;

    public static explicit operator StronglyTypedId<TEntity>(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

using System;
using System.Globalization;
using System.Windows;

namespace Helix_Studio.AppHost;

/// <summary>
/// 本地化（字串資源）Runtime。
/// </summary>
public sealed class LocalizationRuntime
{
    /// <summary>
    /// 依資源鍵取得字串；若找不到則回傳鍵本身。
    /// </summary>
    public string QueryString(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return string.Empty;
        }

        return Application.Current?.TryFindResource(resourceKey) as string ?? resourceKey;
    }

    /// <summary>
    /// 依資源鍵取得格式化字串並套用參數。
    /// </summary>
    public string Format(string resourceKey, params object[] args)
    {
        var format = QueryString(resourceKey);
        return args.Length == 0
            ? format
            : string.Format(CultureInfo.CurrentCulture, format, args);
    }
}

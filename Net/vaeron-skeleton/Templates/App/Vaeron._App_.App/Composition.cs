using Microsoft.Extensions.DependencyInjection;
using Vaeron._App_.Feature;

namespace Vaeron._App_.App;

/// <summary>
/// 組裝的唯一位置。只有註冊,沒有判斷。
/// 註冊順序即背景服務的啟動順序,停止為反向。
/// </summary>
internal static class Composition
{
    public static void Register(IServiceCollection services)
    {
        // 需要共用能力時在此加入：services.Add{Capability}();
        services.AddFeature();
    }
}

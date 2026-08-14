using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Vaeron.HelixStudio.Data;

/// <summary>共用層的 DbContext 接受各功能組件貢獻的 view 註冊。</summary>
/// <remarks>
/// 貢獻者由組裝期的服務容器取得，不另開建構子——多一個建構子會使 DbContext 工廠找不到唯一入口。
/// 模型在第一次使用時建立一次，貢獻者於組裝期即固定，故不需自訂模型快取鍵。
/// </remarks>
public partial class HelixStudioContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        var parts = new[] { this.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>() }
            .Where(extension => extension != null)
            .Select(extension => extension!.ApplicationServiceProvider)
            .Where(services => services != null)
            .SelectMany(services => services!.GetServices<IModelParts>());

        foreach (var part in parts)
        {
            part.Apply(modelBuilder);
        }
    }
}

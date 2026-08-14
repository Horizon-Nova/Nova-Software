using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Data;

/// <summary>功能組件把自己要查的 view 註冊進共用層的模型。</summary>
/// <remarks>共用層只認得表；view 屬於哪個功能組件，就由該組件自己貢獻，相依方向不反轉。</remarks>
public interface IModelParts
{
    public void Apply(ModelBuilder builder);
}

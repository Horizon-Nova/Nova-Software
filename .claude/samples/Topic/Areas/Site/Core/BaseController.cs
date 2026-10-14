using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Areas.Site.Core;

/// <summary>Site 區域每一支控制器的底</summary>
/// <remarks>區域與登入檢查掛在這裡，各控制器不重抄；不必登入的動作自己掛 [AllowAnonymous]。</remarks>
[Area("Site")]
[Authorize]
public abstract class BaseController : Controller
{
    /// <summary>票上的帳號編號</summary>
    protected int AccountId => Convert.ToInt32(User.FindFirstValue(ClaimTypes.NameIdentifier));
}

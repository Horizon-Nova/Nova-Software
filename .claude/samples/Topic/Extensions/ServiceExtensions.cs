using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.Site;
using Web.Areas.Site.Service;
using Web.Areas.Site.Store;

namespace Web.Extensions;

public static class ServiceExtensions
{
    /// <summary> DI注入管理 Store 功能 </summary>
    public static IServiceCollection StoreModule(this IServiceCollection services)
    {
        services.AddScoped<TopicStore>();

        return services;
    }

    /// <summary> DI注入管理 Service 功能 </summary>
    public static IServiceCollection ServiceModule(this IServiceCollection services, IConfiguration configuration)
    {
        // 站台的時區：啟動時讀一次，表單送上來的時間照它換成 UTC
        services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(configuration["TimeZone"]!));

        services.AddScoped<TopicService>();

        return services;
    }

    /// <summary> DI注入管理 資料庫連線 </summary>
    public static IServiceCollection DatabaseModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SiteDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Site")));

        return services;
    }

    /// <summary> DI注入管理 路由與防偽權杖 </summary>
    public static IServiceCollection RouteModule(this IServiceCollection services)
    {
        services.AddControllersWithViews(options =>
        {
            // 每一個非安全方法預設就驗防偽權杖，新增動作不必逐個掛屬性。
            options.Filters.Add<AutoValidateAntiforgeryTokenAttribute>();
        });

        return services;
    }

    /// <summary> DI注入管理 帳號的票：範例每一個請求都當成設定 SampleAccountId 那一個帳號；正式站台在這裡掛登入頁發的 Cookie </summary>
    public static IServiceCollection TicketModule(this IServiceCollection services, IConfiguration configuration)
    {
        // 啟動時讀一次；要改就重啟
        var accountId = configuration["SampleAccountId"]!;

        services.AddAuthentication("Sample")
            .AddScheme<SampleSignInOptions, SampleSignIn>("Sample", options => options.AccountId = accountId);

        return services;
    }

    #region 範例登入

    /// <summary>範例的登入設定：當成哪一個帳號</summary>
    public class SampleSignInOptions : AuthenticationSchemeOptions
    {
        /// <summary>帳號編號，取自設定 SampleAccountId</summary>
        public string AccountId { get; set; } = string.Empty;
    }

    /// <summary>範例的登入：每一個請求都當成同一個帳號，票上只有帳號編號，BaseController 讀的就是這一欄</summary>
    public class SampleSignIn(IOptionsMonitor<SampleSignInOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<SampleSignInOptions>(options, logger, encoder)
    {
        /// <summary>發一張只有帳號編號的票</summary>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Options.AccountId)], Scheme.Name)),
                Scheme.Name)));
    }

    #endregion
}

using Microsoft.Extensions.FileProviders;
using Web.Extensions;

// 範例主題能自己跑起來的最小站台。照 deploy/init/10_site.sql 開好本機庫之後 dotnet run，打開 http://localhost:5593。
// 主題本身在 Areas/Site/ 底下，注入在 Extensions/ 底下，路徑與命名空間和正式專案同一個樣子。
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .RouteModule()
    .TicketModule(builder.Configuration)
    .DatabaseModule(builder.Configuration)
    .StoreModule()
    .ServiceModule(builder.Configuration);

var app = builder.Build();

// 版面的樣式與指令碼直接讀正式站台的 wwwroot，範例不另存一份。
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["SiteAssets"]!))),
});

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area=Site}/{controller=Topic}/{action=Items}");

app.Run();

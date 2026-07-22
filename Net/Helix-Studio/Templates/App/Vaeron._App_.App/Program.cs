namespace Vaeron._App_.App;

internal static class Program
{
    private const string AppName = "_App_";

    private static async Task<int> Main(string[] args)
    {
        using var host = HostSetup.Create(args, AppName).Build();
        await host.RunAsync();
        return 0;
    }
}

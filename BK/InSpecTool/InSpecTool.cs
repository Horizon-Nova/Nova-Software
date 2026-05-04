using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HN_Studio.ModuleSdk;

namespace ExternalModules.InSpecTool;

public sealed class InSpecTool : IHnModule
{
    private readonly Views.InSpecToolView _view = new();
    private HnModuleContext? _context;

    public FrameworkElement QueryView() => _view;

    public ValueTask StartAsync(HnModuleContext context, CancellationToken cancellationToken = default)
    {
        _context = context;
        _view.ApplyHostContext(context);
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        _context = null;
        _view.ClearHostContext();
        return ValueTask.CompletedTask;
    }
}

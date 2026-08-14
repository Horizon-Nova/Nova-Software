using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vaeron._App_.Feature;

/// <summary>Feature 畫面的繫結來源。對上宣告 View 可用的狀態與命令，對下呼叫 Facade 取得資料。</summary>
public sealed partial class FeatureViewModel(IFeatureFacade facade) : ObservableObject
{
    #region 控制項

    // 每個屬性對應 XAML 裡一個實際繫結目標——反查依據是 .xaml 的 {Binding X}。

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _items = [];

    [ObservableProperty]
    private bool _isBusy;

    #endregion

    #region 外部操作方法

    // 被 code-behind 單次直接呼叫（View 的 Loaded 事件），無 +=/-= 配對關係——
    // 跟「生命週期」的區別在這裡：這個方法不訂閱任何事件。

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;

        var result = await facade.LoadAsync(cancellationToken);

        if (result.IsSuccess)
        {
            Title = result.Value!.Title;
            Items = result.Value!.Items;
        }

        IsBusy = false;
    }

    #endregion

    #region 命令

    // 反查依據是 .xaml 的 Command="{Binding RefreshCommand}"。

    [RelayCommand]
    private Task RefreshAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    #endregion
}

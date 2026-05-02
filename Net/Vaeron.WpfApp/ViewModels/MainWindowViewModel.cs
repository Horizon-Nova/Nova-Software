using System.Reflection;
using System.Windows;
using Vaeron.WpfApp.ViewModels.Base;

namespace Vaeron.WpfApp.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public string WindowTitle { get; } = "Vaeron WPF Template";

    public string Heading { get; } = "範本已就緒";

    public string Message { get; } =
        "此範本可直接執行，不改名也能使用。\n" +
        "若要改專案名稱，建議使用 IDE 的 Rename / Refactor，並同步更新 .csproj 的 AssemblyName 與 RootNamespace。";

    public string Footer { get; } = $"版本：{Assembly.GetExecutingAssembly().GetName().Version}";

    public RelayCommand CopyProjectNameCommand { get; }

    public RelayCommand OpenRenameHintCommand { get; }

    public MainWindowViewModel()
    {
        CopyProjectNameCommand = new RelayCommand(CopyProjectName);
        OpenRenameHintCommand = new RelayCommand(OpenRenameHint);
    }

    private static void CopyProjectName()
    {
        var projectName = Assembly.GetExecutingAssembly().GetName().Name ?? "Vaeron.WpfApp";
        Clipboard.SetText(projectName);
        MessageBox.Show($"[成功] 已複製：{projectName}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static void OpenRenameHint()
    {
        MessageBox.Show(
            "改名建議（不使用腳本）：\n" +
            "1. 在 IDE 內對專案/命名空間執行 Rename。\n" +
            "2. 開啟 Vaeron.WpfApp.csproj，更新 AssemblyName 與 RootNamespace。\n" +
            "3. 重新建置確認可執行。\n",
            "如何改名",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}


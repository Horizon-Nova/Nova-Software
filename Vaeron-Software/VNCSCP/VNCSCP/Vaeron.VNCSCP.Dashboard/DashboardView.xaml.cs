using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Vaeron.Kernel.Secrets;
using Vaeron.VNCSCP.Internal.Wpf.Components;

namespace Vaeron.VNCSCP.Dashboard;

public partial class DashboardView : UserControl
{
    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private DashboardViewModel Model => (DashboardViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e) => Model.ApplyViewLoaded();

    private void OnUnloaded(object sender, RoutedEventArgs e) => Model.ApplyViewUnloaded();

    private async void OnAddConnectionClick(object sender, RoutedEventArgs e)
    {
        using var password = new SecretBuffer(Encoding.UTF8.GetBytes(AddPasswordBox.Password));
        await Model.ApplyCreateConnectionCommand.ExecuteAsync(password);

        foreach (var _ in new[] { AddPasswordBox }.Where(_2 => Model.IsAddRowExpanded is false))
        {
            AddPasswordBox.Clear();
        }
    }

    /// <summary>儲存編輯。密碼留空代表不變更，存成功才清空輸入框。</summary>
    private async void OnSaveEditClick(object sender, RoutedEventArgs e)
    {
        foreach (var button in new[] { sender }.OfType<FrameworkElement>())
        {
            foreach (var row in new[] { button.DataContext }.OfType<DashboardConnectionRow>())
            {
                var boxes = FindRowDescendants<RevealablePasswordBox>(button).ToList();
                using var password = boxes.Select(box => box.TryGetSecretBuffer()).FirstOrDefault();
                var saved = await Model.ApplySaveEditAsync(row, password);

                foreach (var box in boxes.Where(_ => saved))
                {
                    box.Clear();
                }
            }
        }
    }

    /// <summary>還原欄位成目前連線的值，並清空密碼欄位。</summary>
    private void OnResetEditClick(object sender, RoutedEventArgs e)
    {
        foreach (var button in new[] { sender }.OfType<FrameworkElement>())
        {
            foreach (var row in new[] { button.DataContext }.OfType<DashboardConnectionRow>())
            {
                row.ApplyResetEditFields();
            }

            foreach (var box in FindRowDescendants<RevealablePasswordBox>(button))
            {
                box.Clear();
            }
        }
    }

    /// <summary>展開時帶出目前密碼、收合時清掉。Click 早於 Command 執行，此時列上的展開狀態仍是切換前的值。</summary>
    private async void OnToggleExpandClick(object sender, RoutedEventArgs e)
    {
        foreach (var button in new[] { sender }.OfType<FrameworkElement>())
        {
            foreach (var row in new[] { button.DataContext }.OfType<DashboardConnectionRow>())
            {
                var boxes = FindRowDescendants<RevealablePasswordBox>(button).ToList();

                foreach (var box in boxes.Where(_ => row.IsExpanded))
                {
                    box.Clear();
                }

                foreach (var _ in boxes.Where(_2 => row.IsExpanded is false).Take(1))
                {
                    var plaintext = await Model.ApplyRevealPasswordAsync(row.Id);
                    plaintext.ToList().ForEach(text => boxes.ForEach(target => target.PrefillMasked(text)));
                }
            }
        }
    }

    private void OnBrowseDeployFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "選擇要部署的檔案" };

        foreach (var path in new[] { dialog }.Where(picker => picker.ShowDialog() is true).Select(picker => picker.FileName))
        {
            Model.DeployLocalSourcePath = path;
        }
    }

    private void OnBrowseDeployFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "選擇要部署的資料夾" };

        foreach (var path in new[] { dialog }.Where(picker => picker.ShowDialog() is true).Select(picker => picker.FolderName))
        {
            Model.DeployLocalSourcePath = path;
        }
    }

    /// <summary>同一列的欄位散在多個平行容器底下，往上逐層擴大搜尋範圍才找得到。</summary>
    private static IEnumerable<T> FindRowDescendants<T>(DependencyObject start) where T : DependencyObject
        => Ancestors(start)
            .Select(ancestor => Descendants(ancestor).OfType<T>().ToList())
            .Where(found => found.Count > 0)
            .DefaultIfEmpty([])
            .First();

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject start)
    {
        var current = VisualTreeHelper.GetParent(start);

        while (current is not null)
        {
            yield return current;
            current = VisualTreeHelper.GetParent(current);
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        => Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(root))
            .Select(index => VisualTreeHelper.GetChild(root, index))
            .SelectMany(child => new[] { child }.Concat(Descendants(child)));
}


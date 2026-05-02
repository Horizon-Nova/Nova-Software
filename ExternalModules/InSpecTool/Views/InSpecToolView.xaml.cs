using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ExternalModules.InSpecTool.Core;
using ExternalModules.InSpecTool.Services;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.InSpecTool.Views;

public partial class InSpecToolView : UserControl
{
    public static readonly DependencyProperty LastClickedFilePathProperty = DependencyProperty.Register(
        nameof(LastClickedFilePath),
        typeof(string),
        typeof(InSpecToolView),
        new PropertyMetadata(null));

    private readonly InSpecToolService _toolService = new();
    private readonly InSpecAutomationService _automationService = new();
    private readonly InSpecFileBridgeService _fileBridgeService = new();
    private readonly DispatcherTimer _barcodeDebounceTimer = new();
    private IShellContext? _hostContext;
    private InSpecToolConfiguration _configuration = new();
    private bool _suspendBarcodeRefresh;
    private bool _suspendFieldRefresh;
    private int _searchVersion;

    public ObservableCollection<PreviewFieldItem> Fields { get; } = [];

    public ObservableCollection<PreviewResultItem> Results { get; } = [];

    public string? LastClickedFilePath
    {
        get => (string?)GetValue(LastClickedFilePathProperty);
        set => SetValue(LastClickedFilePathProperty, value);
    }

    public InSpecToolView()
    {
        InitializeComponent();
        DataContext = this;
        _barcodeDebounceTimer.Tick += BarcodeDebounceTimer_OnTick;
        _automationService.AutoInputRequested += AutomationService_OnAutoInputRequested;
        UpdateBarcodeInputPlaceholderVisibility();
        ReloadConfigurationAndRefresh(clearFileIndex: true);
        Unloaded += (_, _) =>
        {
            _automationService.Stop();
            _fileBridgeService.Stop();
        };
    }

    internal void ApplyHostContext(IShellContext context)
    {
        _hostContext = context;
        ReloadConfigurationAndRefresh(clearFileIndex: true);
    }

    internal void ClearHostContext()
    {
        _hostContext = null;
        _automationService.Stop();
        ReloadConfigurationAndRefresh(clearFileIndex: true);
    }

    private void BarcodeInputBox_OnTextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        UpdateBarcodeInputPlaceholderVisibility();

        if (_suspendBarcodeRefresh)
        {
            return;
        }

        RestartBarcodeDebounce();
    }

    private void BarcodeInputBox_OnFocusChanged(object sender, RoutedEventArgs eventArgs)
    {
        UpdateBarcodeInputPlaceholderVisibility();
    }

    private void UpdateBarcodeInputPlaceholderVisibility()
    {
        BarcodeInputPlaceholderText.Visibility = string.IsNullOrWhiteSpace(BarcodeInputBox.Text) && !BarcodeInputBox.IsKeyboardFocused
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void FileResultListView_OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        var current = eventArgs.OriginalSource as DependencyObject;
        while (current != null && current is not ListViewItem)
        {
            current = VisualTreeHelper.GetParent(current);
        }

        if (current is ListViewItem { DataContext: PreviewResultItem item })
        {
            LastClickedFilePath = item.FilePath;
        }
    }

    private async void QueryRefreshButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        _toolService.ClearFileIndex();
        await RefreshFromBarcodeAsync();
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        BarcodeInputBox.Clear();
        UpdateBarcodeInputPlaceholderVisibility();
    }

    private async void BarcodeDebounceTimer_OnTick(object? sender, EventArgs eventArgs)
    {
        _barcodeDebounceTimer.Stop();
        await RefreshFromBarcodeAsync();
    }

    private void RestartBarcodeDebounce()
    {
        _barcodeDebounceTimer.Stop();
        _barcodeDebounceTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, _configuration.BarcodeDebounceMs));
        _barcodeDebounceTimer.Start();
    }

    private void ReloadConfigurationAndRefresh(bool clearFileIndex)
    {
        _configuration = _toolService.QueryConfiguration(_hostContext);
        _barcodeDebounceTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, _configuration.BarcodeDebounceMs));
        _fileBridgeService.Start();
        _fileBridgeService.UpdateCurrentTargetPath(_toolService.ResolveCurrentTargetPathForManualOutput(Fields, _configuration));

        if (_hostContext is null)
        {
            _automationService.Stop();
            AutomationStatusText.Text = "自動監控：待模組啟用";
        }
        else
        {
            _automationService.UpdateConfiguration(_configuration);
            _automationService.Start();
            ApplyAutomationStatus();
        }

        if (clearFileIndex)
        {
            _toolService.ClearFileIndex();
        }

        _ = RefreshFromBarcodeAsync();
    }

    private async Task RefreshFromBarcodeAsync()
    {
        var parseResult = _toolService.QueryBarcodeParseResult(BarcodeInputBox.Text ?? string.Empty, _configuration.FieldDefinitions);
        var fieldItems = _toolService.QueryFieldItems(parseResult.RawBarcode, _configuration.FieldDefinitions);
        RebuildFields(fieldItems);
        await RefreshResultsAsync(parseResult);
    }

    private async Task RefreshFromFieldsAsync()
    {
        var combinedBarcode = _toolService.ComposeBarcode(Fields, _configuration.FieldDefinitions);

        _suspendBarcodeRefresh = true;
        BarcodeInputBox.Text = combinedBarcode;
        _suspendBarcodeRefresh = false;
        await RefreshResultsAsync(null);
    }

    private void RebuildFields(IReadOnlyList<PreviewFieldItem> fieldItems)
    {
        foreach (var field in Fields)
        {
            field.PropertyChanged -= FieldItem_OnPropertyChanged;
        }

        _suspendFieldRefresh = true;
        Fields.Clear();

        foreach (var fieldItem in fieldItems)
        {
            fieldItem.PropertyChanged += FieldItem_OnPropertyChanged;
            Fields.Add(fieldItem);
        }

        _suspendFieldRefresh = false;
    }

    private async Task RefreshResultsAsync(InSpecBarcodeParseResult? parseResult)
    {
        var barcodeValidationMessage = parseResult?.QueryValidationMessage() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(barcodeValidationMessage))
        {
            Results.Clear();
            _fileBridgeService.UpdateCurrentTargetPath(null);
            ApplySearchingState(false);
            ApplyPlaceholderState(barcodeValidationMessage);
            return;
        }

        var searchVersion = ++_searchVersion;
        var fieldSnapshot = Fields
            .Select(field => new PreviewFieldItem
            {
                Label = field.Label,
                Value = field.Value,
                IsReadOnly = field.IsReadOnly,
                Keyword = field.Keyword,
                TargetMode = field.TargetMode
            })
            .ToArray();

        ApplySearchingState(true);
        var queryResult = await Task.Run(() => _toolService.QueryResults(fieldSnapshot, _configuration));
        if (searchVersion != _searchVersion)
        {
            return;
        }

        Results.Clear();
        foreach (var resultItem in queryResult.Results)
        {
            Results.Add(resultItem);
        }

        _fileBridgeService.UpdateCurrentTargetPath(_toolService.ResolveCurrentTargetPathForManualOutput(fieldSnapshot, _configuration));

        ApplySearchingState(false);
        ApplyPlaceholderState(queryResult.PlaceholderText);
    }

    private void ApplySearchingState(bool isSearching)
    {
        FileListTitleText.Text = isSearching ? "檔案結果（搜尋中...）" : "檔案結果";
    }

    private void ApplyPlaceholderState(string placeholderText)
    {
        var showPlaceholder = !string.IsNullOrWhiteSpace(placeholderText) && Results.Count == 0;
        FileListPlaceholderText.Text = placeholderText;
        FileListPlaceholderText.Visibility = showPlaceholder ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyAutomationStatus()
    {
        AutomationStatusText.Text = _configuration.EnableAutoMonitoring
            ? $"自動監控：開啟（{Math.Clamp(_configuration.MonitorIntervalMs, 100, 10000)} ms）"
            : "自動監控：關閉";
    }

    private async void FieldItem_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (_suspendFieldRefresh || eventArgs.PropertyName != nameof(PreviewFieldItem.Value))
        {
            return;
        }

        await RefreshFromFieldsAsync();
    }

    private void FileResultListView_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        if (FileResultListView.SelectedItem is not PreviewResultItem resultItem)
        {
            return;
        }

        if (!_toolService.QueryCanOpenFile(Fields, out var message))
        {
            MessageBox.Show(message, "InSpec 工具", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _toolService.OpenFile(resultItem, _configuration);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "InSpec 工具", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AutomationService_OnAutoInputRequested(object? sender, string inputTypeName)
    {
        _ = Dispatcher.InvokeAsync(() => HandleAutoInputRequest(inputTypeName));
    }

    private void HandleAutoInputRequest(string inputTypeName)
    {
        var definition = _toolService.QueryDefinition(_configuration.FieldDefinitions, inputTypeName);
        if (definition is null)
        {
            return;
        }

        var field = Fields.FirstOrDefault(item => string.Equals(item.Label, inputTypeName, StringComparison.Ordinal));
        var currentValue = field?.Value?.Trim() ?? string.Empty;

        if (definition.QueryRequiresConfirmation())
        {
            var confirmedValue = InSpecValuePromptWindow.Show(Window.GetWindow(this), inputTypeName, currentValue);
            if (string.IsNullOrWhiteSpace(confirmedValue))
            {
                return;
            }

            if (!_automationService.TryAutoInput(confirmedValue))
            {
                MessageBox.Show("自動輸入失敗，請確認 InSpec 對話框仍在前景且可操作。", "InSpec 工具", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(currentValue))
        {
            MessageBox.Show($"請先在欄位「{inputTypeName}」填入內容。", "InSpec 工具", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_automationService.TryAutoInput(currentValue))
        {
            MessageBox.Show("自動輸入失敗，請確認 InSpec 對話框仍在前景且可操作。", "InSpec 工具", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

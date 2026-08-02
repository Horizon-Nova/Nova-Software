using CommunityToolkit.Mvvm.ComponentModel;

namespace Vaeron.HelixStudio;

/// <summary>目前嵌入畫面裡的工具視窗狀態。同時只嵌入一個工具，切到別的工具時整個換掉。</summary>
public sealed partial class ToolHostViewModel : ObservableObject
{
    [ObservableProperty] private string? _toolName;
    [ObservableProperty] private nint _windowHandle;

    public bool IsWaiting => WindowHandle == 0;

    partial void OnWindowHandleChanged(nint value) => OnPropertyChanged(nameof(IsWaiting));
}

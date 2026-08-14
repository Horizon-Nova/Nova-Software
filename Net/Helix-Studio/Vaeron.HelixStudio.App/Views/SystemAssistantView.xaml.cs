using System.Windows.Controls;
using Vaeron.HelixStudio.Sampling;
using Vaeron.HelixStudio.SystemAssistant;

namespace Vaeron.HelixStudio.App.Views;

public partial class SystemAssistantView : UserControl
{
    private readonly SystemAssistantViewModel viewModel;

    public SystemAssistantView(SystemAssistantViewModel model, ISamplerFacade sampler)
    {
        InitializeComponent();
        viewModel = model;
        DataContext = model;
        model.LoadCommand.Execute(null);

        sampler.Subscribe(stamp => Dispatcher.Invoke(() => model.OnSampled(stamp)));
    }

    /// <summary>離開輸入框才寫回草稿：打字途中重查會重建集合，游標會跳掉。</summary>
    private void OnFieldCommitted(object sender, System.Windows.RoutedEventArgs e)
    {
        var box = (TextBox)sender;
        var field = (FieldRowView)box.DataContext;
        viewModel.EditField(field.SoftwareId, field.FieldKey, box.Text);
    }
}

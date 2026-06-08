using System.Windows.Controls;
using HorizonNova.SampleModule.ViewModels;

namespace HorizonNova.SampleModule.Views;

public partial class SampleModuleView : UserControl
{
    public SampleModuleView(SampleModulePageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

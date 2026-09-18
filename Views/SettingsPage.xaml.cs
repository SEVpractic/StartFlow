using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using StartFlow.ViewModels;

namespace StartFlow.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            viewModel.XamlRoot = XamlRoot;
        }
    }
}
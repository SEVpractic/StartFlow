using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StartFlow.Services;
using StartFlow.ViewModels;
using StartFlow.Views;

namespace StartFlow;

public sealed partial class MainWindow : Window
{
    private readonly ConfigurationService _configService;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly SettingsViewModel _settingsViewModel;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(770, 780));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        App.MainWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);

        _configService = App.Configuration;
        _configService.Load();

        ApplyTheme(_configService.CurrentConfig?.Theme ?? "default");

        var startupService = new StartupService(_configService);
        _dashboardViewModel = new DashboardViewModel(_configService, startupService);
        _settingsViewModel = new SettingsViewModel(_configService);

        _dashboardViewModel.Refresh();
        NavFrame.Navigate(typeof(DashboardPage), _dashboardViewModel);
    }

    public void ApplyTheme(string theme)
    {
        RootGrid.RequestedTheme = theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        NavFrame.GoBack();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        switch (item.Tag?.ToString())
        {
            case "dashboard":
                _dashboardViewModel.Refresh();
                NavFrame.Navigate(typeof(DashboardPage), _dashboardViewModel);
                break;
            case "settings":
                _settingsViewModel.Load();
                NavFrame.Navigate(typeof(SettingsPage), _settingsViewModel);
                break;
        }
    }
}
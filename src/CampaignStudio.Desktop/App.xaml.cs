using Microsoft.UI.Xaml;

namespace CampaignStudio.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? window;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new MainWindow(); window.Activate();
        if (Environment.GetCommandLineArgs().Contains("--minimized") && window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter) presenter.Minimize();
    }
}

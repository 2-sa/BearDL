using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Nickvision.Desktop.Application;
using Nickvision.Parabolic.WinUI.Helpers;
using Nickvision.Parabolic.WinUI.Views;
using System;
using WinRT;

namespace Nickvision.Parabolic.WinUI;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private Window? _window;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        GlassThemeHelper.Initialize(
            _serviceProvider.GetRequiredService<IConfigurationService>(),
            _serviceProvider.GetRequiredService<AppInfo>().IsPortable);
        UnhandledException += (_, e) =>
        {
            _serviceProvider.GetRequiredService<ILogger<App>>().LogError(e.Exception, $"An unhandled exception occurred: {e.Message}");
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_window is null)
        {
            var window = _serviceProvider.GetRequiredService<MainWindow>();
            _window = window;
            Program.AttachActivationHandler(uri => window.DispatcherQueue.TryEnqueue(() =>
            {
                BringToFront(window);
                if (uri is not null)
                {
                    window.ReceiveExternalUrl(uri);
                }
            }));
        }
        BringToFront(_window);
    }

    private static void BringToFront(Window window)
    {
        var presenter = window.AppWindow.Presenter as OverlappedPresenter
            ?? (window.AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped
                ? window.AppWindow.Presenter.As<OverlappedPresenter>()
                : null);
        if (presenter is not null && presenter.State == OverlappedPresenterState.Minimized)
        {
            presenter.Restore();
        }
        window.AppWindow.Show();
        window.Activate();
    }
}

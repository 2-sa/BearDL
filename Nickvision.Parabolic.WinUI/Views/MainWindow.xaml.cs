using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using Microsoft.UI.Windowing;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.Network;
using Nickvision.Desktop.Notifications;
using Nickvision.Desktop.WinUI.Helpers;
using Nickvision.Parabolic.Shared.Controllers;
using Nickvision.Parabolic.Shared.Events;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.Shared.Services;
using Nickvision.Parabolic.WinUI.Controls;
using Nickvision.Parabolic.WinUI.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.Graphics;
using WinRT;

namespace Nickvision.Parabolic.WinUI.Views;

public sealed partial class MainWindow : Window
{
    private enum Pages
    {
        Home = 0,
        Downloads,
        Custom
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly MainWindowController _controller;
    private readonly AppInfo _appInfo;
    private readonly ITranslationService _translationService;
    private readonly Dictionary<int, DownloadRow> _downloadRows;
    private readonly List<int> _downloadOrder = new();
    private readonly ObservableCollection<DownloadRow> _visibleDownloadRows = new();
    private readonly DispatcherQueueTimer _notificationTimer;
    private readonly DispatcherQueueTimer _clipboardDebounceTimer;
    private readonly List<RectInt32> _titleBarInputRegions = new(8);
    private bool _titleBarUpdateQueued;
    private bool _isClosed;
    private bool _isClosingConfirmOpen;
    private readonly TaskCompletionSource<bool> _startupReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _addDialogGate = new(1, 1);
    private readonly CancellationTokenSource _windowClosed = new();
    private string? _clipboardSuggestedUrl;
    private string? _clipboardDismissedUrl;
    private NavigationViewItem? _lastDownloadsNavItem;
    private RoutedEventHandler? _notificationClickHandler;

    public MainWindow(IServiceProvider serviceProvider, MainWindowController controller, AppInfo appInfo, IEventsService eventsService, ITranslationService translationService)
    {
        InitializeComponent();
        // Localize the inner layout; the window root stays LTR for native hit testing.
        LocalizationHelper.Apply(MainGrid, translationService);
        _serviceProvider = serviceProvider;
        _controller = controller;
        _appInfo = appInfo;
        _translationService = translationService;
        _downloadRows = new Dictionary<int, DownloadRow>();
        ListDownloads.ItemsSource = _visibleDownloadRows;
        _notificationTimer = DispatcherQueue.CreateTimer();
        _notificationTimer.Interval = TimeSpan.FromSeconds(5);
        _notificationTimer.IsRepeating = false;
        _notificationTimer.Tick += (_, _) => InfoBar.IsOpen = false;
        _clipboardDebounceTimer = DispatcherQueue.CreateTimer();
        _clipboardDebounceTimer.Interval = TimeSpan.FromMilliseconds(150);
        _clipboardDebounceTimer.IsRepeating = false;
        _clipboardDebounceTimer.Tick += async (_, _) => await RefreshClipboardSuggestionAsync();
        InfoBar.Closed += (_, _) =>
        {
            _notificationTimer.Stop();
            if (_notificationClickHandler is not null)
            {
                BtnInfoBar.Click -= _notificationClickHandler;
                _notificationClickHandler = null;
            }
            BtnInfoBar.Visibility = Visibility.Collapsed;
        };
        Closed += (_, _) =>
        {
            _isClosed = true;
            _windowClosed.Cancel();
            _notificationTimer.Stop();
            _clipboardDebounceTimer.Stop();
            GlassThemeHelper.LayoutModeChanged -= OnLayoutModeChanged;
        };
        GlassThemeHelper.LayoutModeChanged += OnLayoutModeChanged;
        _notificationClickHandler = null;
        // Config
        ApplyTheme();
        this.Geometry = _controller.WindowGeometry;
        // TitleBar
        AppWindow.SetIcon(_appInfo.Version!.IsPreview ? "./Assets/org.nickvision.tubeconverter-devel.ico" : "./Assets/org.nickvision.tubeconverter.ico");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        TitleBar.LayoutUpdated += (_, _) => QueueTitleBarInputRegionUpdate();
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        if (MainGrid.FlowDirection == FlowDirection.RightToLeft)
        {
            // XAML direction does not move the native Windows caption buttons.
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
            RtlCaptionButtons.Visibility = Visibility.Visible;
            SetCaptionLabel(BtnWindowClose, _translationService._("Close"));
            SetCaptionLabel(BtnWindowMinimize, _translationService._("Minimize"));
            UpdateMaximizeButton();
            AppWindow.Changed += (_, _) => UpdateMaximizeButton();
        }
        BtnPreview.Visibility = _appInfo.Version.IsPreview ? Visibility.Visible : Visibility.Collapsed;
        // Events
        AppWindow.Closing += Window_Closing;
        eventsService.AppNotificationSent += (sender, e) => DispatcherQueue.TryEnqueue(() => App_AppNotificationSent(sender, e));
        eventsService.ConfigurationSaved += App_ConfigurationSaved;
        eventsService.DownloadAdded += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadAdded(sender, e));
        eventsService.DownloadProgressChanged += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadProgressChanged(sender, e));
        eventsService.DownloadCompleted += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadCompleted(sender, e));
        eventsService.DownloadStopped += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadStopped(sender, e));
        eventsService.DownloadStartedFromQueue += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadStartedFromQueue(sender, e));
        eventsService.DownloadRetired += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadRetired(sender, e));
        eventsService.DownloadRequested += (s, args) => DispatcherQueue.TryEnqueue(() => ReceiveExternalUrl(args.Url));
        // Translations
        AppWindow.Title = _appInfo.ShortName;
        LblTitle.Text = _appInfo.ShortName;
        MenuFile.Title = _translationService._("File");
        MenuAddDownload.Text = _translationService._("Add Download");
        MenuExit.Text = _translationService._("Exit");
        MenuEdit.Title = _translationService._("Edit");
        MenuKeyring.Text = _translationService._("Keyring");
        MenuSettings.Text = _translationService._("Settings");
        MenuView.Title = _translationService._("View");
        MenuHistory.Text = _translationService._("History");
        MenuDownloads.Title = _translationService._("Downloads");
        MenuStopAllRemaining.Text = _translationService._("Stop All Remaining");
        MenuRetryAllFailed.Text = _translationService._("Retry All Failed");
        MenuClearAllQueued.Text = _translationService._("Clear All Queued");
        MenuClearAllCompleted.Text = _translationService._("Clear All Completed");
        MenuHelp.Title = _translationService._("Help");
        MenuCheckForUpdates.Text = _translationService._("Check for Updates");
        MenuGitHubRepo.Text = _translationService._("GitHub Repo");
        MenuReportABug.Text = _translationService._("Report a Bug");
        MenuDiscussions.Text = _translationService._("Discussions");
        MenuAbout.Text = _translationService._("About {0}", _appInfo.ShortName!);
        MenuPopupCheckForUpdates.Text = _translationService._("Check for Updates");
        MenuPopupGitHubRepo.Text = _translationService._("GitHub Repo");
        MenuPopupReportABug.Text = _translationService._("Report a Bug");
        MenuPopupDiscussions.Text = _translationService._("Discussions");
        MenuPopupAbout.Text = _translationService._("About {0}", _appInfo.ShortName!);
        ToolTipService.SetToolTip(BtnPreview, _translationService._("You are running a preview version of {0}", _appInfo.ShortName!));
        LblPreview.Text = _translationService._("Thank you for testing the upcoming features and changes! ❤️");
        LblHomeTitle.Text = _translationService._("Download Media");
        LblHomeDescription.Text = _translationService._("Add a video, audio, or playlist URL to start downloading");
        LblAddDownload.Text = _translationService._("Add Download");
        LblKeyring.Text = _translationService._("Keyring");
        LblSettings.Text = _translationService._("Settings");
        NavHistory.Content = _translationService._("History");
        NavKeyring.Content = _translationService._("Keyring");
        NavSettings.Content = _translationService._("Settings");
        NavAbout.Content = _translationService._("Help");
        TxtQuickUrl.PlaceholderText = _translationService._("Paste a video, audio, or playlist URL");
        ToolTipService.SetToolTip(BtnQuickPaste, _translationService._("Paste"));
        AutomationProperties.SetName(BtnQuickPaste, _translationService._("Paste"));
        ToolTipService.SetToolTip(BtnQuickClear, _translationService._("Clear"));
        AutomationProperties.SetName(BtnQuickClear, _translationService._("Clear"));
        LblQuickDownload.Text = _translationService._("Download");
        ToolTipService.SetToolTip(BtnQuickDownload, _translationService._("Quick Download"));
        LblQuickCustomize.Text = _translationService._("Customize");
        ToolTipService.SetToolTip(BtnQuickCustomize, _translationService._("Customize"));
        LblClipboardPrompt.Text = _translationService._("Link in clipboard:");
        LblClipboardUse.Text = _translationService._("Download");
        ToolTipService.SetToolTip(BtnClipboardDismiss, _translationService._("Dismiss"));
        LblEmptyTitle.Text = _translationService._("Start by pasting a link");
        LblEmptySubtitle.Text = _translationService._("Supports videos, audio, and entire playlists");
        LblHintPaste.Text = _translationService._("Paste anywhere");
        LblHintDrag.Text = _translationService._("Drag a link onto the window");
        LblHintEnter.Text = _translationService._("Quick download at best quality");
        LblModernEmptyAddDownload.Text = _translationService._("Add Download");
        UpdateSaveFolderCue();
        BtnStopAllRemaining.Label = _translationService._("Stop All Remaining");
        BtnRetryAllFailed.Label = _translationService._("Retry All Failed");
        BtnClearAllQueued.Label = _translationService._("Clear All Queued");
        BtnClearAllCompleted.Label = _translationService._("Clear All Completed");
        BtnModernStopAllRemaining.Label = _translationService._("Stop All Remaining");
        BtnModernRetryAllFailed.Label = _translationService._("Retry All Failed");
        BtnModernClearAllQueued.Label = _translationService._("Clear All Queued");
        BtnModernClearAllCompleted.Label = _translationService._("Clear All Completed");
        LblDownloadsAddDownload.Text = _translationService._("Add");
        NavDownloadsAll.Content = _translationService._("All");
        NavDownloadsRunning.Content = _translationService._("Running");
        NavDownloadsQueued.Content = _translationService._("Queued");
        NavDownloadsCompleted.Content = _translationService._("Completed");
        NavDownloadsFailed.Content = _translationService._("Failed");
        StatusNoneDownloads.Title = _translationService._("No Downloads");
        StatusNoneDownloads.Description = _translationService._("There are no downloads of this type");
        LblNoneAddDownload.Text = _translationService._("Add Download");
        DlgCredential.Title = _translationService._("Credential Required");
        TxtCredentialUsername.PlaceholderText = _translationService._("Enter username here");
        TxtCredentialPassword.PlaceholderText = _translationService._("Enter password here");
        DlgCredential.PrimaryButtonText = _translationService._("Submit");
        DlgCredential.CloseButtonText = _translationService._("Cancel");
        // Modern layout shortcuts: Ctrl+L focuses the link bar, Ctrl+V pastes a link from anywhere.
        MainGrid.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        var focusUrlAccelerator = new KeyboardAccelerator { Key = VirtualKey.L, Modifiers = VirtualKeyModifiers.Control };
        focusUrlAccelerator.Invoked += FocusUrlAccelerator_Invoked;
        MainGrid.KeyboardAccelerators.Add(focusUrlAccelerator);
        var pasteAccelerator = new KeyboardAccelerator { Key = VirtualKey.V, Modifiers = VirtualKeyModifiers.Control };
        pasteAccelerator.Invoked += PasteAccelerator_Invoked;
        MainGrid.KeyboardAccelerators.Add(pasteAccelerator);
        // Offer a copied link when the user comes back to BearDL or copies while it is focused.
        Activated += (_, args) =>
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                ScheduleClipboardRefresh();
            }
        };
        Clipboard.ContentChanged += Clipboard_ContentChanged;
        Closed += (_, _) => Clipboard.ContentChanged -= Clipboard_ContentChanged;
        _lastDownloadsNavItem = NavDownloadsAll;
        ApplyLayoutMode(GlassThemeHelper.UseModernLayout, true);
    }

    private async void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        DialogQueueHelper.RegisterDispatcher(MainGrid.XamlRoot, DispatcherQueue);
        ViewStack.SelectedIndex = GlassThemeHelper.UseModernLayout ? (int)Pages.Downloads : (int)Pages.Home;
        ViewStackDownloads.SelectedIndex = 0;
        MenuCheckForUpdates.IsEnabled = false;
        MenuPopupCheckForUpdates.IsEnabled = false;
        var updatesTask = _controller.CheckForUpdatesAsync(false);
        if (_controller.ShowDisclaimerOnStartup)
        {
            var checkBox = new CheckBox()
            {
                Content = _translationService._("Don't show this message again")
            };
            var disclaimerDialog = new ContentDialog()
            {
                Title = _translationService._("Legal Copyright Disclaimer"),
                Content = new StackPanel()
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock()
                        {
                            Text = _translationService._("Videos on YouTube and other sites may be subject to DMCA protection. The authors of BearDL do not endorse, and are not responsible for, the use of this application in means that will violate these laws."),
                            TextWrapping = TextWrapping.WrapWholeWords
                        },
                        checkBox
                    }
                },
                CloseButtonText = _translationService._("I understand"),
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = MainGrid.ActualTheme,
                FlowDirection = MainGrid.FlowDirection,
                XamlRoot = MainGrid.XamlRoot
            };
            await DialogQueueHelper.ShowQueuedAsync(disclaimerDialog, MainGrid.XamlRoot, _windowClosed.Token);
            if (checkBox.IsChecked ?? false)
            {
                _controller.ShowDisclaimerOnStartup = false;
            }
        }
        if (_controller.RecoverableDownloadsCount > 0)
        {
            if (await DialogQueueHelper.ShowConfirmAsync(
                    MainGrid.XamlRoot,
                    _translationService._("Recover Downloads?"),
                    _translationService._("There are downloads available to recover from when BearDL crashed. Would you like to download them again?"),
                    _translationService._("Yes"),
                    _translationService._("No"),
                    MainGrid.FlowDirection,
                    MainGrid.ActualTheme,
                    ContentDialogButton.Primary,
                    _windowClosed.Token))
            {
                await _controller.RecoverAllDownloadsAsync();
            }
            else
            {
                await _controller.ClearRecoverableDownloadsAsync();
            }
        }
        _startupReady.TrySetResult(true);
        if (_controller.UrlFromArgs is not null)
        {
            await AddDownloadAsync(_controller.UrlFromArgs);
        }
        try
        {
            await updatesTask;
        }
        finally
        {
            MenuCheckForUpdates.IsEnabled = true;
            MenuPopupCheckForUpdates.IsEnabled = true;
        }
    }

    private async void Window_Closing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        if (!_controller.CanShutdown)
        {
            if (e is not null)
            {
                e.Cancel = true;
            }
            if (_isClosingConfirmOpen)
            {
                return;
            }
            _isClosingConfirmOpen = true;
            try
            {
                if (await DialogQueueHelper.ShowConfirmAsync(
                        MainGrid.XamlRoot,
                        _appInfo.ShortName ?? "BearDL",
                        _translationService._("There are downloads still in progress. Would you like to stop them and exit?"),
                        _translationService._("Yes"),
                        _translationService._("No"),
                        MainGrid.FlowDirection,
                        MainGrid.ActualTheme,
                        ContentDialogButton.Close,
                        _windowClosed.Token))
                {
                    _controller.WindowGeometry = this.Geometry;
                    await _controller.StopAllDownloadsAsync();
                    Close();
                    _serviceProvider.GetRequiredService<IHostApplicationLifetime>().StopApplication();
                }
            }
            finally
            {
                _isClosingConfirmOpen = false;
            }
            return;
        }
        _controller.WindowGeometry = this.Geometry;
        _serviceProvider.GetRequiredService<IHostApplicationLifetime>().StopApplication();
    }

    private void App_AppNotificationSent(object? sender, AppNotificationSentEventArgs e)
    {
        _notificationTimer.Stop();
        if (_notificationClickHandler is not null)
        {
            BtnInfoBar.Click -= _notificationClickHandler;
            _notificationClickHandler = null;
        }
        InfoBar.Message = e.Notification.Message;
        InfoBar.Severity = e.Notification.Severity switch
        {
            NotificationSeverity.Success => InfoBarSeverity.Success,
            NotificationSeverity.Warning => InfoBarSeverity.Warning,
            NotificationSeverity.Error => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational
        };
        if (e.Notification.Action == "update")
        {
            BtnInfoBar.Content = _translationService._("Update");
            _notificationClickHandler = WindowsUpdate;
            BtnInfoBar.Click += _notificationClickHandler;
        }
        else if (e.Notification.Action == "update-ytdlp")
        {
            BtnInfoBar.Content = _translationService._("Update");
            _notificationClickHandler = YtdlpUpdate;
            BtnInfoBar.Click += _notificationClickHandler;
        }
        else if (e.Notification.Action == "update-deno")
        {
            BtnInfoBar.Content = _translationService._("Update");
            _notificationClickHandler = DenoUpdate;
            BtnInfoBar.Click += _notificationClickHandler;
        }
        else if (e.Notification.Action == "error" && !string.IsNullOrEmpty(e.Notification.ActionParam))
        {
            BtnInfoBar.Content = _translationService._("Details");
            _notificationClickHandler = async (_, _) =>
            {
                InfoBar.IsOpen = false;
                var errorDialog = new ContentDialog()
                {
                    Title = _translationService._("Error"),
                    Content = new ScrollViewer()
                    {
                        FlowDirection = FlowDirection.LeftToRight,
                        Padding = new Thickness(0, 0, 16, 0),
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                        Content = new TextBlock()
                        {
                            Text = e.Notification.ActionParam,
                            TextWrapping = TextWrapping.Wrap
                        }
                    },
                    CloseButtonText = _translationService._("Close"),
                    DefaultButton = ContentDialogButton.Close,
                    RequestedTheme = MainGrid.ActualTheme,
                    FlowDirection = MainGrid.FlowDirection,
                    XamlRoot = MainGrid.XamlRoot
                };
                await DialogQueueHelper.ShowQueuedAsync(errorDialog, MainGrid.XamlRoot, _windowClosed.Token);
            };
            BtnInfoBar.Click += _notificationClickHandler;
        }
        BtnInfoBar.Visibility = _notificationClickHandler is not null ? Visibility.Visible : Visibility.Collapsed;
        InfoBar.IsOpen = true;
        _notificationTimer.Start();
    }

    private void App_ConfigurationSaved(object? sender, ConfigurationSavedEventArgs args)
    {
        if (args.ChangedPropertyName == "Theme")
        {
            DispatcherQueue.TryEnqueue(ApplyTheme);
        }
        if (string.IsNullOrEmpty(args.ChangedPropertyName) || args.ChangedPropertyName == "SaveFolder")
        {
            DispatcherQueue.TryEnqueue(UpdateSaveFolderCue);
        }
    }

    private void ApplyTheme()
    {
        AppWindow.TitleBar.PreferredTheme = _controller.Theme switch
        {
            Theme.Light => TitleBarTheme.Light,
            Theme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode
        };
        var requestedTheme = _controller.Theme switch
        {
            Theme.Light => ElementTheme.Light,
            Theme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        // Window.SystemBackdrop (MicaBackdrop) and PopupRoot observe the root
        // Window.Content element rather than child grids.
        WindowRoot.RequestedTheme = requestedTheme;
        MainGrid.RequestedTheme = requestedTheme;
    }

    private void QueueTitleBarInputRegionUpdate()
    {
        if (_titleBarUpdateQueued || _isClosed)
        {
            return;
        }
        _titleBarUpdateQueued = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _titleBarUpdateQueued = false;
            if (!_isClosed)
            {
                UpdateTitleBarInputRegions();
            }
        }))
        {
            _titleBarUpdateQueued = false;
        }
    }

    private void UpdateTitleBarInputRegions()
    {
        if (TitleBar.XamlRoot is null)
        {
            return;
        }
        // WinUI 2.0 marks the whole header as interactive, including empty
        // space beside the menus. Only actual menus and buttons need input.
        _titleBarInputRegions.Clear();
        var scale = TitleBar.XamlRoot.RasterizationScale;
        CollectTitleBarControls(TitleBar, scale);
        CollectTitleBarControls(RtlCaptionButtons, scale);
        var inputSource = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        if (!inputSource.GetRegionRects(NonClientRegionKind.Passthrough).SequenceEqual(_titleBarInputRegions))
        {
            inputSource.SetRegionRects(NonClientRegionKind.Passthrough, _titleBarInputRegions.ToArray());
        }
    }

    private void CollectTitleBarControls(DependencyObject element, double scale)
    {
        if (element is UIElement uiElement &&
            (uiElement.Visibility != Visibility.Visible || !uiElement.IsHitTestVisible))
        {
            return;
        }
        if (element is FrameworkElement control && (control is Button || control is MenuBar))
        {
            if (control.ActualWidth > 0 && control.ActualHeight > 0)
            {
                var bounds = control.TransformToVisual((UIElement)Content).TransformBounds(
                    new Windows.Foundation.Rect(0, 0, control.ActualWidth, control.ActualHeight));
                var left = (int)Math.Floor(bounds.Left * scale);
                var top = (int)Math.Floor(bounds.Top * scale);
                _titleBarInputRegions.Add(new RectInt32
                {
                    X = left,
                    Y = top,
                    Width = (int)Math.Ceiling(bounds.Right * scale) - left,
                    Height = (int)Math.Ceiling(bounds.Bottom * scale) - top
                });
            }
            return;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            CollectTitleBarControls(VisualTreeHelper.GetChild(element, i), scale);
        }
    }

    private static void SetCaptionLabel(Button button, string label)
    {
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label);
        ToolTipService.SetToolTip(button, label);
    }

    private OverlappedPresenter? GetOverlappedPresenter()
    {
        var presenter = AppWindow.Presenter;
        // The WinRT object can be projected as its base AppWindowPresenter type
        // in NativeAOT. Query its native interface instead of silently failing
        // a managed "is OverlappedPresenter" check.
        return presenter.Kind == AppWindowPresenterKind.Overlapped
            ? presenter.As<OverlappedPresenter>()
            : null;
    }

    private void UpdateMaximizeButton()
    {
        if (GetOverlappedPresenter() is { } presenter)
        {
            var maximized = presenter.State == OverlappedPresenterState.Maximized;
            IconWindowMaximize.Glyph = maximized ? "\uE923" : "\uE922";
            SetCaptionLabel(BtnWindowMaximize, maximized ? _translationService._("Restore") : _translationService._("Maximize"));
        }
    }

    private void WindowMinimize_Click(object sender, RoutedEventArgs e)
    {
        if (GetOverlappedPresenter() is { } presenter)
        {
            presenter.Minimize();
        }
    }

    private void WindowMaximize_Click(object sender, RoutedEventArgs e)
    {
        if (GetOverlappedPresenter() is { } presenter)
        {
            if (presenter.State == OverlappedPresenterState.Maximized)
            {
                presenter.Restore();
            }
            else
            {
                presenter.Maximize();
            }
        }
    }

    private void OnLayoutModeChanged(object? sender, bool useModern)
    {
        DispatcherQueue.TryEnqueue(() => ApplyLayoutMode(useModern, false));
    }

    private void ApplyLayoutMode(bool useModern, bool isInitial)
    {
        if (useModern)
        {
            TopMenuBar.Width = 0;
            TopMenuBar.Height = 0;
            TopMenuBar.Opacity = 0;
            TopMenuBar.IsHitTestVisible = false;
            TopSeparator.Visibility = Visibility.Collapsed;
            ClassicDownloadsToolbar.Visibility = Visibility.Collapsed;
            ClassicDownloadsSeparator.Visibility = Visibility.Collapsed;
            NavFooterSeparator.Visibility = Visibility.Visible;
            NavSaveFolder.Visibility = Visibility.Visible;
            NavHistory.Visibility = Visibility.Visible;
            NavKeyring.Visibility = Visibility.Visible;
            NavSettings.Visibility = Visibility.Visible;
            NavAbout.Visibility = Visibility.Visible;
            QuickUrlCard.Visibility = Visibility.Visible;
            UpdateSaveFolderCue();
            if (!isInitial && ViewStack.SelectedIndex == (int)Pages.Home)
            {
                ViewStack.SelectedIndex = (int)Pages.Downloads;
            }
            ScheduleClipboardRefresh();
        }
        else
        {
            TopMenuBar.Width = double.NaN;
            TopMenuBar.Height = double.NaN;
            TopMenuBar.Opacity = 1;
            TopMenuBar.IsHitTestVisible = true;
            TopSeparator.Visibility = Visibility.Visible;
            ClassicDownloadsToolbar.Visibility = Visibility.Visible;
            ClassicDownloadsSeparator.Visibility = Visibility.Visible;
            NavFooterSeparator.Visibility = Visibility.Collapsed;
            NavSaveFolder.Visibility = Visibility.Collapsed;
            NavHistory.Visibility = Visibility.Collapsed;
            NavKeyring.Visibility = Visibility.Collapsed;
            NavSettings.Visibility = Visibility.Collapsed;
            NavAbout.Visibility = Visibility.Collapsed;
            QuickUrlCard.Visibility = Visibility.Collapsed;
            ModernDownloadsToolbar.Visibility = Visibility.Collapsed;
            _clipboardDebounceTimer.Stop();
            ClipboardSuggestion.Visibility = Visibility.Collapsed;
            if (!isInitial && ViewStack.SelectedIndex == (int)Pages.Downloads && _downloadRows.Count == 0)
            {
                ViewStack.SelectedIndex = (int)Pages.Home;
            }
        }
        UpdateDownloadsList();
        QueueTitleBarInputRegionUpdate();
    }

    private void UpdateSaveFolderCue()
    {
        var folder = _controller.SaveFolder;
        var trimmed = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var displayName = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = trimmed;
        }
        NavSaveFolder.Content = string.IsNullOrEmpty(displayName)
            ? _translationService._("Save Folder")
            : displayName;
        var tooltip = string.IsNullOrEmpty(folder)
            ? _translationService._("Save Folder")
            : $"{_translationService._("Save Folder")}: {folder}";
        ToolTipService.SetToolTip(NavSaveFolder, tooltip);
        AutomationProperties.SetName(NavSaveFolder, tooltip);
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        TitleBar.IsBackButtonVisible = false;
        FrameCustom.Content = null;
        if (GlassThemeHelper.UseModernLayout)
        {
            ViewStack.SelectedIndex = (int)Pages.Downloads;
            NavViewDownloads.SelectedItem = _lastDownloadsNavItem ?? NavDownloadsAll;
        }
        else
        {
            ViewStack.SelectedIndex = _downloadRows.Count > 0 ? (int)Pages.Downloads : (int)Pages.Home;
        }
    }

    private async void Controller_DownloadAdded(object? sender, DownloadAddedEventArgs e)
    {
        var row = _serviceProvider.GetRequiredService<DownloadRow>();
        row.PauseRequested += DownloadRow_PauseRequested;
        row.ResumeRequested += DownloadRow_ResumeRequested;
        row.StopRequested += DownloadRow_StopRequested;
        row.RetryRequested += DownloadRow_RetryRequested;
        await row.TriggerAddedStateAsync(e);
        if (_isClosed)
        {
            DetachDownloadRowEvents(row);
            return;
        }
        _downloadRows[e.Id] = row;
        _downloadOrder.Add(e.Id);
        UpdateDownloadsList();
        if (ViewStack.SelectedIndex != (int)Pages.Custom)
        {
            ViewStack.SelectedIndex = (int)Pages.Downloads;
            TitleBar.IsBackButtonVisible = false;
        }
    }

    private void Controller_DownloadCompleted(object? sender, DownloadCompletedEventArgs e)
    {
        if (_downloadRows.TryGetValue(e.Id, out var row))
        {
            row.TriggerCompletedState(e);
            UpdateDownloadsList();
        }
    }

    private void Controller_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        if (_downloadRows.TryGetValue(e.Id, out var row))
        {
            row.TriggerProgressState(e);
        }
    }

    private void Controller_DownloadStartedFromQueue(object? sender, DownloadEventArgs e)
    {
        if (_downloadRows.TryGetValue(e.Id, out var row))
        {
            row.TriggerStartedFromQueueState();
            UpdateDownloadsList();
        }
    }

    private void Controller_DownloadStopped(object? sender, DownloadEventArgs e)
    {
        if (_downloadRows.TryGetValue(e.Id, out var row))
        {
            row.TriggerStoppedState();
            UpdateDownloadsList();
        }
    }

    private void Controller_DownloadRetired(object? sender, DownloadEventArgs e)
    {
        if (RemoveDownloadRow(e.Id))
        {
            UpdateDownloadsList();
        }
    }

    private bool RemoveDownloadRow(int id)
    {
        if (!_downloadRows.Remove(id, out var row))
        {
            return false;
        }
        _downloadOrder.Remove(id);
        DetachDownloadRowEvents(row);
        return true;
    }

    private void DetachDownloadRowEvents(DownloadRow row)
    {
        row.PauseRequested -= DownloadRow_PauseRequested;
        row.ResumeRequested -= DownloadRow_ResumeRequested;
        row.StopRequested -= DownloadRow_StopRequested;
        row.RetryRequested -= DownloadRow_RetryRequested;
    }

    private void DownloadRow_PauseRequested(object? sender, int id)
    {
        if (_controller.PauseDownload(id) && _downloadRows.TryGetValue(id, out var row))
        {
            row.TriggerPausedState();
        }
    }

    private void DownloadRow_ResumeRequested(object? sender, int id)
    {
        if (_controller.ResumeDownload(id) && _downloadRows.TryGetValue(id, out var row))
        {
            row.TriggerResumedState();
        }
    }

    private async void DownloadRow_RetryRequested(object? sender, int id) => await _controller.RetryDownloadAsync(id);

    private async void DownloadRow_StopRequested(object? sender, int id) => await _controller.StopDownloadAsync(id);

    private void NavViewDownloads_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem selectedItem)
        {
            var tag = selectedItem.Tag as string;
            if (tag is "0" or "1" or "2" or "3" or "4")
            {
                _lastDownloadsNavItem = selectedItem;
                TitleBar.IsBackButtonVisible = false;
            }
        }
        // NavigationView can raise SelectionChanged during its own measure pass (initial
        // IsSelected item). Changing ViewStack content synchronously there throws
        // "Child collection must not be modified during measure or arrange", so defer.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_isClosed)
            {
                return;
            }
            if (GlassThemeHelper.UseModernLayout && ViewStack.SelectedIndex != (int)Pages.Downloads && ViewStack.SelectedIndex != (int)Pages.Custom)
            {
                ViewStack.SelectedIndex = (int)Pages.Downloads;
            }
            UpdateDownloadsList();
        });
    }

    private async void NavViewDownloads_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var tag = args.InvokedItemContainer?.Tag as string;
        if (tag == "SaveFolder")
        {
            var folder = _controller.SaveFolder;
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                await Launcher.LaunchFolderPathAsync(folder);
            }
        }
        else if (tag == "History")
        {
            History(sender, new RoutedEventArgs());
        }
        else if (tag == "Keyring")
        {
            Keyring(sender, new RoutedEventArgs());
        }
        else if (tag == "Settings")
        {
            OpenSettingsView();
        }
        else if (tag == "Help")
        {
            FlyoutHelp.Placement = MainGrid.FlowDirection == FlowDirection.RightToLeft
                ? Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.TopEdgeAlignedRight
                : Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.TopEdgeAlignedLeft;
            Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase.ShowAttachedFlyout(NavAbout);
        }
        else if (tag == "About")
        {
            About(sender, new RoutedEventArgs());
        }
    }

    private void TxtQuickUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = TxtQuickUrl.Text;
        BtnQuickClear.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        var isValid = text.TryExtractHttpUri(out _);
        BtnQuickDownload.IsEnabled = isValid;
        BtnQuickCustomize.IsEnabled = isValid;
    }

    private async void TxtQuickUrl_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && BtnQuickDownload.IsEnabled)
        {
            e.Handled = true;
            await TriggerQuickDownloadAsync();
        }
    }

    private void QuickClear_Click(object sender, RoutedEventArgs e)
    {
        TxtQuickUrl.Text = string.Empty;
        TxtQuickUrl.Focus(FocusState.Programmatic);
    }

    private async void QuickPaste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text))
            {
                var text = (await content.GetTextAsync())?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    TxtQuickUrl.Text = text.TryExtractHttpUri(out var uri) ? uri.ToString() : text;
                    TxtQuickUrl.Focus(FocusState.Programmatic);
                    TxtQuickUrl.SelectAll();
                }
            }
        }
        catch
        {
        }
    }

    private async void QuickDownload_Click(object sender, RoutedEventArgs e) => await TriggerQuickDownloadAsync();

    private async Task TriggerQuickDownloadAsync()
    {
        if (!TxtQuickUrl.Text.TryExtractHttpUri(out var uri))
        {
            return;
        }
        TxtQuickUrl.Text = string.Empty;
        await StartHeadlessFastDownloadAsync(uri);
    }

    private async Task StartHeadlessFastDownloadAsync(Uri uri)
    {
        var controller = _serviceProvider.GetRequiredService<AddDownloadDialogController>();
        var xamlRoot = MainGrid.XamlRoot;
        var flowDirection = MainGrid.FlowDirection;
        var requestedTheme = MainGrid.ActualTheme;
        DialogQueueHelper.RegisterDispatcher(xamlRoot, DispatcherQueue);
        controller.ConfirmDuplicateDownloadAsync = msg => DialogQueueHelper.ConfirmDuplicateDownloadAsync(
            xamlRoot,
            msg,
            _translationService,
            flowDirection,
            requestedTheme,
            _windowClosed.Token);
        await controller.AddFastDownloadsAsync(uri, null, _controller.SaveFolder);
    }

    private async void QuickCustomize_Click(object sender, RoutedEventArgs e)
    {
        if (!TxtQuickUrl.Text.TryExtractHttpUri(out var uri))
        {
            return;
        }
        TxtQuickUrl.Text = string.Empty;
        await RunAddDialogAsync(dialog => dialog.ShowCustomDiscoverAsync(uri));
    }

    private void FocusUrlAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (GlassThemeHelper.UseModernLayout && QuickUrlCard.Visibility == Visibility.Visible)
        {
            TxtQuickUrl.Focus(FocusState.Keyboard);
            TxtQuickUrl.SelectAll();
            args.Handled = true;
        }
    }

    private async void PasteAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var focused = FocusManager.GetFocusedElement(MainGrid.XamlRoot);
        if (focused is TextBox || focused is PasswordBox)
        {
            return;
        }
        if (!GlassThemeHelper.UseModernLayout)
        {
            return;
        }
        try
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text))
            {
                var text = (await content.GetTextAsync())?.Trim();
                if (!string.IsNullOrEmpty(text) && text.TryExtractHttpUri(out var uri))
                {
                    TxtQuickUrl.Text = uri.ToString();
                    TxtQuickUrl.Focus(FocusState.Keyboard);
                    TxtQuickUrl.SelectAll();
                    args.Handled = true;
                }
            }
        }
        catch
        {
        }
    }

    private void ScheduleClipboardRefresh()
    {
        if (_isClosed || !GlassThemeHelper.UseModernLayout)
        {
            _clipboardDebounceTimer.Stop();
            return;
        }
        _clipboardDebounceTimer.Stop();
        _clipboardDebounceTimer.Start();
    }

    private void Clipboard_ContentChanged(object? sender, object e)
    {
        DispatcherQueue.TryEnqueue(ScheduleClipboardRefresh);
    }

    private async Task RefreshClipboardSuggestionAsync()
    {
        if (!GlassThemeHelper.UseModernLayout || _isClosed)
        {
            ClipboardSuggestion.Visibility = Visibility.Collapsed;
            return;
        }
        try
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text))
            {
                var text = (await content.GetTextAsync())?.Trim();
                if (_isClosed || !GlassThemeHelper.UseModernLayout)
                {
                    ClipboardSuggestion.Visibility = Visibility.Collapsed;
                    return;
                }
                if (!string.IsNullOrEmpty(text)
                    && text.TryExtractHttpUri(out var uri))
                {
                    var extractedUrl = uri.ToString();
                    if (extractedUrl != _clipboardDismissedUrl)
                    {
                        _clipboardSuggestedUrl = extractedUrl;
                        LblClipboardUrl.Text = extractedUrl;
                        ClipboardSuggestion.Visibility = Visibility.Visible;
                        return;
                    }
                }
            }
        }
        catch
        {
        }
        _clipboardSuggestedUrl = null;
        ClipboardSuggestion.Visibility = Visibility.Collapsed;
    }

    private async void ClipboardUse_Click(object sender, RoutedEventArgs e)
    {
        if (_clipboardSuggestedUrl is null || !_clipboardSuggestedUrl.TryExtractHttpUri(out var uri))
        {
            return;
        }
        _clipboardDismissedUrl = _clipboardSuggestedUrl;
        _clipboardSuggestedUrl = null;
        ClipboardSuggestion.Visibility = Visibility.Collapsed;
        var probeController = _serviceProvider.GetRequiredService<AddDownloadDialogController>();
        if (probeController.FastDownload)
        {
            await StartHeadlessFastDownloadAsync(uri);
            return;
        }
        await RunAddDialogAsync(dialog => dialog.ShowCustomDiscoverAsync(uri));
    }

    private void ClipboardDismiss_Click(object sender, RoutedEventArgs e)
    {
        _clipboardDismissedUrl = _clipboardSuggestedUrl;
        ClipboardSuggestion.Visibility = Visibility.Collapsed;
    }

    private void Workspace_DragOver(object sender, DragEventArgs e)
    {
        if (!GlassThemeHelper.UseModernLayout)
        {
            return;
        }
        if (e.DataView.Contains(StandardDataFormats.Text) || e.DataView.Contains(StandardDataFormats.Uri))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = _translationService._("Add Download");
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }
    }

    private async void Workspace_Drop(object sender, DragEventArgs e)
    {
        if (!GlassThemeHelper.UseModernLayout)
        {
            return;
        }
        string? url = null;
        try
        {
            if (e.DataView.Contains(StandardDataFormats.Uri))
            {
                var uri = await e.DataView.GetUriAsync();
                url = uri?.ToString();
            }
            else if (e.DataView.Contains(StandardDataFormats.Text))
            {
                url = (await e.DataView.GetTextAsync())?.Trim();
            }
        }
        catch
        {
        }
        if (!string.IsNullOrEmpty(url) && url.TryExtractHttpUri(out var dropUri))
        {
            TxtQuickUrl.Text = dropUri.ToString();
            TxtQuickUrl.Focus(FocusState.Keyboard);
            TxtQuickUrl.SelectAll();
        }
    }

    private void UpdateProgress_Changed(object? sender, DownloadProgress e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (e.Completed)
            {
                FlyoutUpdateProgress.Hide();
                BtnUpdateProgress.Visibility = Visibility.Collapsed;
                return;
            }
            var message = _translationService._("Downloading update: {0}%", Math.Round(e.Percentage * 100));
            BtnUpdateProgress.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(BtnUpdateProgress, message);
            RingUpdateProcess.Value = e.Percentage * 100;
            LblUpdateProgress.Text = message;
        });
    }

    private async void About(object? sender, RoutedEventArgs e)
    {
        await DialogQueueHelper.RunQueuedAsync(async () =>
        {
            var progressDialog = new ContentDialog()
            {
                Title = _translationService._("About {0}", _appInfo.ShortName!),
                Content = new ProgressRing()
                {
                    IsActive = true,
                },
                RequestedTheme = MainGrid.ActualTheme,
                FlowDirection = MainGrid.FlowDirection,
                XamlRoot = MainGrid.XamlRoot
            };
            var debugTask = _controller.GetDebugInformationAsync();
            var isProgressOpen = false;
            void OnProgressOpened(ContentDialog senderDialog, ContentDialogOpenedEventArgs args)
            {
                isProgressOpen = true;
                if (debugTask.IsCompleted)
                {
                    senderDialog.Hide();
                }
            }
            progressDialog.Opened += OnProgressOpened;
            _ = debugTask.ContinueWith(_ => DispatcherQueue.TryEnqueue(() =>
            {
                if (isProgressOpen)
                {
                    progressDialog.Hide();
                }
            }), TaskScheduler.Default);
            var progressShowTask = progressDialog.ShowAsync().AsTask();
            var debugInfo = await debugTask;
            if (isProgressOpen)
            {
                progressDialog.Hide();
            }
            await progressShowTask;
            progressDialog.Opened -= OnProgressOpened;
            var aboutDialog = _serviceProvider.GetRequiredService<AboutDialog>();
            aboutDialog.DebugInformation = debugInfo;
            aboutDialog.RequestedTheme = MainGrid.ActualTheme;
            aboutDialog.XamlRoot = MainGrid.XamlRoot;
            await DialogQueueHelper.WaitForOpenDialogsAsync(MainGrid.XamlRoot, _windowClosed.Token);
            await aboutDialog.ShowAsync();
        }, MainGrid.XamlRoot, _windowClosed.Token);
    }

    private async void AddDownload(object? sender, RoutedEventArgs e) => await AddDownloadAsync(null);

    private void Exit(object sender, RoutedEventArgs args) => Window_Closing(AppWindow, null!);

    private async void Keyring(object sender, RoutedEventArgs args)
    {
        var keyringDialog = _serviceProvider.GetRequiredService<KeyringDialog>();
        keyringDialog.RequestedTheme = MainGrid.ActualTheme;
        keyringDialog.XamlRoot = MainGrid.XamlRoot;
        await DialogQueueHelper.ShowQueuedAsync(() => keyringDialog.ShowAsync(), MainGrid.XamlRoot, _windowClosed.Token);
    }

    private void Settings(object sender, RoutedEventArgs args) => OpenSettingsView();

    private void OpenSettingsView()
    {
        TitleBar.IsBackButtonVisible = true;
        ViewStack.SelectedIndex = (int)Pages.Custom;
        var settings = _serviceProvider.GetRequiredService<SettingsPage>();
        settings.WindowId = AppWindow.Id;
        FrameCustom.Content = settings;
    }

    private async void History(object sender, RoutedEventArgs args)
    {
        var historyDialog = _serviceProvider.GetRequiredService<HistoryDialog>();
        historyDialog.RequestedTheme = MainGrid.ActualTheme;
        historyDialog.XamlRoot = MainGrid.XamlRoot;
        await DialogQueueHelper.ShowQueuedAsync(() => historyDialog.ShowAsync(), MainGrid.XamlRoot, _windowClosed.Token);
    }

    private async void CheckForUpdates(object? sender, RoutedEventArgs e)
    {
        MenuCheckForUpdates.IsEnabled = false;
        MenuPopupCheckForUpdates.IsEnabled = false;
        try
        {
            await _controller.CheckForUpdatesAsync(true);
        }
        finally
        {
            MenuCheckForUpdates.IsEnabled = true;
            MenuPopupCheckForUpdates.IsEnabled = true;
        }
    }

    private void ClearAllCompleted(object? sender, RoutedEventArgs e)
    {
        foreach (var id in _controller.ClearCompletedDownloads())
        {
            RemoveDownloadRow(id);
        }
        UpdateDownloadsList();
    }

    private async void ClearAllQueued(object? sender, RoutedEventArgs e)
    {
        foreach (var id in await _controller.ClearQueuedDownloadsAsync())
        {
            RemoveDownloadRow(id);
        }
        UpdateDownloadsList();
    }

    private async void DenoUpdate(object? sender, RoutedEventArgs e)
    {
        var progress = new Progress<DownloadProgress>();
        progress.ProgressChanged += UpdateProgress_Changed;
        InfoBar.IsOpen = false;
        await _controller.DenoUpdateAsync(progress);
        progress.ProgressChanged -= UpdateProgress_Changed;
    }

    private async void Discussions(object? sender, RoutedEventArgs e) => await LaunchUriAsync(_appInfo.DiscussionsForum);

    private async void GitHubRepo(object? sender, RoutedEventArgs e) => await LaunchUriAsync(_appInfo.SourceRepository);

    private async void ReportABug(object? sender, RoutedEventArgs e) => await LaunchUriAsync(_appInfo.IssueTracker);

    private async void RetryAllFailed(object? sender, RoutedEventArgs e) => await _controller.RetryFailedDownloadsAsync();

    private async void StopAllRemaining(object? sender, RoutedEventArgs e) => await _controller.StopAllDownloadsAsync();

    private async void WindowsUpdate(object? sender, RoutedEventArgs e)
    {
        var progress = new Progress<DownloadProgress>();
        progress.ProgressChanged += UpdateProgress_Changed;
        InfoBar.IsOpen = false;
        await _controller.WindowsUpdateAsync(progress);
        progress.ProgressChanged -= UpdateProgress_Changed;
    }

    private async void YtdlpUpdate(object? sender, RoutedEventArgs e)
    {
        var progress = new Progress<DownloadProgress>();
        progress.ProgressChanged += UpdateProgress_Changed;
        InfoBar.IsOpen = false;
        await _controller.YtdlpUpdateAsync(progress);
        progress.ProgressChanged -= UpdateProgress_Changed;
    }

    public async void ReceiveExternalUrl(Uri uri)
    {
        try
        {
            await AddDownloadAsync(uri);
        }
        catch (Exception error)
        {
            _serviceProvider.GetRequiredService<INotificationService>().Send(new AppNotification(_translationService._("An error occurred while discovering media"), NotificationSeverity.Error)
            {
                Action = "error",
                ActionParam = error.ToString()
            });
        }
    }

    private Task AddDownloadAsync(Uri? uri)
    {
        return RunAddDialogAsync(dialog => uri is not null ? dialog.ShowAsync(uri) : dialog.ShowAsync());
    }

    private async Task RunAddDialogAsync(Func<AddDownloadDialog, Task> showDialogAsync)
    {
        var acquired = false;
        try
        {
            await _startupReady.Task.WaitAsync(_windowClosed.Token);
            await _addDialogGate.WaitAsync(_windowClosed.Token);
            acquired = true;
            await DialogQueueHelper.WaitForOpenDialogsAsync(MainGrid.XamlRoot, _windowClosed.Token);
            _windowClosed.Token.ThrowIfCancellationRequested();
            var addDownloadDialog = _serviceProvider.GetRequiredService<AddDownloadDialog>();
            addDownloadDialog.WindowId = AppWindow.Id;
            addDownloadDialog.RequestedTheme = MainGrid.ActualTheme;
            addDownloadDialog.XamlRoot = MainGrid.XamlRoot;
            await showDialogAsync(addDownloadDialog);
        }
        catch (OperationCanceledException) when (_windowClosed.IsCancellationRequested)
        {
        }
        finally
        {
            if (acquired)
            {
                _addDialogGate.Release();
            }
        }
    }

    private async Task LaunchUriAsync(Uri? uri)
    {
        if (uri is null)
        {
            return;
        }
        await Launcher.LaunchUriAsync(uri);
    }

    private void UpdateDownloadsList()
    {
        var activeFilterItem = (NavViewDownloads.SelectedItem as NavigationViewItem);
        if (activeFilterItem is null || activeFilterItem.Tag as string is not ("0" or "1" or "2" or "3" or "4"))
        {
            activeFilterItem = _lastDownloadsNavItem ?? NavDownloadsAll;
        }
        var selectedTag = (activeFilterItem.Tag as string) ?? "0";
        var targetRows = new List<DownloadRow>(_downloadRows.Count);
        var targetSet = new HashSet<DownloadRow>();
        for (var i = _downloadOrder.Count - 1; i >= 0; i--)
        {
            if (_downloadRows.TryGetValue(_downloadOrder[i], out var row) && selectedTag switch
            {
                "1" => row.Status == DownloadStatus.Running || row.Status == DownloadStatus.Paused,
                "2" => row.Status == DownloadStatus.Queued,
                "3" => row.Status == DownloadStatus.Success || row.Status == DownloadStatus.Error || row.Status == DownloadStatus.Stopped,
                "4" => row.Status == DownloadStatus.Error,
                _ => true
            })
            {
                targetRows.Add(row);
                targetSet.Add(row);
            }
        }
        for (var i = _visibleDownloadRows.Count - 1; i >= 0; i--)
        {
            if (!targetSet.Contains(_visibleDownloadRows[i]))
            {
                _visibleDownloadRows.RemoveAt(i);
            }
        }
        for (var i = 0; i < targetRows.Count; i++)
        {
            var desired = targetRows[i];
            if (i < _visibleDownloadRows.Count && ReferenceEquals(_visibleDownloadRows[i], desired))
            {
                continue;
            }
            var existingIndex = -1;
            for (var j = i + 1; j < _visibleDownloadRows.Count; j++)
            {
                if (ReferenceEquals(_visibleDownloadRows[j], desired))
                {
                    existingIndex = j;
                    break;
                }
            }
            if (existingIndex >= 0)
            {
                _visibleDownloadRows.RemoveAt(existingIndex);
            }
            _visibleDownloadRows.Insert(i, desired);
        }
        var downloadsPage = targetRows.Count > 0 ? 1 : 0;
        if (ViewStackDownloads.SelectedIndex != downloadsPage)
        {
            ViewStackDownloads.SelectedIndex = downloadsPage;
        }
        if (GlassThemeHelper.UseModernLayout)
        {
            LblModernSectionTitle.Text = activeFilterItem.Content as string ?? _translationService._("All");
            LblModernSectionCount.Text = targetRows.Count > 0 ? $"· {targetRows.Count}" : string.Empty;
            BtnClassicNoneAdd.Visibility = Visibility.Collapsed;

            if (_downloadRows.Count == 0)
            {
                ModernDownloadsToolbar.Visibility = Visibility.Collapsed;
                ModernEmptyState.Visibility = Visibility.Visible;
                ScrollDownloads.Visibility = Visibility.Collapsed;
            }
            else
            {
                ModernDownloadsToolbar.Visibility = Visibility.Visible;
                ModernEmptyState.Visibility = Visibility.Collapsed;
                ScrollDownloads.Visibility = Visibility.Visible;
                StatusNoneDownloads.Description = _translationService._("There are no downloads of this type");
            }

            BtnModernStopAllRemaining.Visibility = _controller.RemainingDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnModernRetryAllFailed.Visibility = _controller.FailedDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnModernClearAllQueued.Visibility = _controller.QueuedDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnModernClearAllCompleted.Visibility = _controller.CompletedDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            BtnModernStopAllRemaining.IsEnabled = _controller.RunningDownloadsCount > 0;
            BtnModernRetryAllFailed.IsEnabled = _controller.FailedDownloadsCount > 0;
            BtnModernClearAllQueued.IsEnabled = _controller.QueuedDownloadsCount > 0;
            BtnModernClearAllCompleted.IsEnabled = _controller.CompletedDownloadsCount > 0;

            var hasModernBatchAction = BtnModernStopAllRemaining.Visibility == Visibility.Visible
                || BtnModernRetryAllFailed.Visibility == Visibility.Visible
                || BtnModernClearAllQueued.Visibility == Visibility.Visible
                || BtnModernClearAllCompleted.Visibility == Visibility.Visible;
            CmdBarModernActions.Visibility = hasModernBatchAction ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            ModernEmptyState.Visibility = Visibility.Collapsed;
            ScrollDownloads.Visibility = Visibility.Visible;
            BtnClassicNoneAdd.Visibility = Visibility.Visible;
            StatusNoneDownloads.Description = _translationService._("There are no downloads of this type");
        }
        InfoBadgeDownloadsAll.Value = _controller.RemainingDownloadsCount;
        InfoBadgeDownloadsAll.Visibility = _controller.RemainingDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        InfoBadgeDownloadsRunning.Value = _controller.RunningDownloadsCount;
        InfoBadgeDownloadsRunning.Visibility = _controller.RunningDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        InfoBadgeDownloadsQueued.Value = _controller.QueuedDownloadsCount;
        InfoBadgeDownloadsQueued.Visibility = _controller.QueuedDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        InfoBadgeDownloadsCompleted.Value = _controller.CompletedDownloadsCount;
        InfoBadgeDownloadsCompleted.Visibility = _controller.CompletedDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        InfoBadgeDownloadsFailed.Value = _controller.FailedDownloadsCount;
        InfoBadgeDownloadsFailed.Visibility = _controller.FailedDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

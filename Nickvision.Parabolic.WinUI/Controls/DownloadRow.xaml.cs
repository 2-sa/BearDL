using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Nickvision.Desktop.Globalization;
using Nickvision.Parabolic.Shared.Events;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.Shared.Services;
using Nickvision.Parabolic.WinUI.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.System;

namespace Nickvision.Parabolic.WinUI.Controls;

public sealed partial class DownloadRow : UserControl
{
    private enum ButtonsPage
    {
        Running = 0,
        Done,
        Error
    }

    private readonly IThumbnailService _thumbnailService;
    private readonly ITranslationService _translator;
    private int _id;
    private string _path;
    private string _log;
    private bool _isPaused;

    public event EventHandler<int>? PauseRequested;
    public event EventHandler<int>? ResumeRequested;
    public event EventHandler<int>? StopRequested;
    public event EventHandler<int>? RetryRequested;

    public DownloadStatus Status { get; private set; }

    public DownloadRow(IThumbnailService thumbnailService, ITranslationService translator)
    {
        InitializeComponent();
        LocalizationHelper.Apply(this, translator);
        _thumbnailService = thumbnailService;
        _translator = translator;
        _id = -1;
        _path = string.Empty;
        _log = string.Empty;
        _isPaused = false;
        BtnShowLog.Text = _translator._("Log");
        ActionsFlyout.MenuFlyoutPresenterStyle = new Style(typeof(MenuFlyoutPresenter))
        {
            Setters = { new Setter(FlowDirectionProperty, FlowDirection) }
        };
        LocalizationHelper.Apply(BtnShowLog, translator);
        SetActionLabel(BtnMore, _translator._("Options"));
        SetActionLabel(BtnPauseResume, _translator._("Pause"));
        SetActionLabel(BtnStop, _translator._("Stop"));
        SetActionLabel(BtnPlay, _translator._("Play"));
        SetActionLabel(BtnOpenFolder, _translator._("Open"));
        AutomationProperties.SetName(BtnCopyFile, _translator._("Copy"));
        CopyFileToolTip.Content = _translator._("Copy the file to paste it into another app");
        SetActionLabel(BtnRetry, _translator._("Retry"));
    }

    private static void SetActionLabel(Button button, string text)
    {
        AutomationProperties.SetName(button, text);
        ToolTipService.SetToolTip(button, text);
    }

    public async Task TriggerAddedStateAsync(DownloadAddedEventArgs args)
    {
        using var thumbnailMemoryStream = await _thumbnailService.GetImageStreamAsync(args.Url);
        using var thumbnailStream = thumbnailMemoryStream.AsRandomAccessStream();
        var thumbnailDecoder = await BitmapDecoder.CreateAsync(thumbnailStream);
        var thumbnailBitmap = await thumbnailDecoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var thumbnailSource = new SoftwareBitmapSource();
        await thumbnailSource.SetBitmapAsync(thumbnailBitmap);
        _id = args.Id;
        _path = args.Path;
        IcnStatus.Glyph = "\uE896";
        ImgThumbnail.Source = thumbnailSource;
        LblTitle.Text = Path.GetFileName(_path);
        ToolTipService.SetToolTip(LblTitle, LblTitle.Text);
        ProgBar.Value = 0.0;
        if (args.Status == DownloadStatus.Queued)
        {
            Status = DownloadStatus.Queued;
            LblStatus.Text = _translator._("Queued");
            ProgBar.IsIndeterminate = false;
        }
        else if (args.Status == DownloadStatus.Running)
        {
            Status = DownloadStatus.Running;
            LblStatus.Text = _translator._("Running");
            ProgBar.IsIndeterminate = true;
        }
        else
        {
            Status = DownloadStatus.Running;
            LblStatus.Text = _translator._("Processing");
            ProgBar.IsIndeterminate = true;
        }
        ViewStackButtons.SelectedIndex = (int)ButtonsPage.Running;
    }

    public void TriggerCompletedState(DownloadCompletedEventArgs args)
    {
        _path = args.Path;
        if (args.Log.Length > 0)
        {
            _log = args.Log.ToString();
        }
        LblTitle.Text = Path.GetFileName(_path);
        ToolTipService.SetToolTip(LblTitle, LblTitle.Text);
        LblLog.Text = _log;
        ScrollLog.ChangeView(null, ScrollLog.ScrollableHeight, null);
        ProgBar.Value = 1.0;
        ProgBar.IsIndeterminate = false;
        if (args.Status == DownloadStatus.Error)
        {
            Status = DownloadStatus.Error;
            IcnStatus.Glyph = "\uEA39";
            LblStatus.Text = _translator._("Error");
            ViewStackButtons.SelectedIndex = (int)ButtonsPage.Error;
        }
        else if (args.Status == DownloadStatus.Success)
        {
            Status = DownloadStatus.Success;
            IcnStatus.Glyph = "\uE930";
            LblStatus.Text = _translator._("Success");
            ViewStackButtons.SelectedIndex = (int)ButtonsPage.Done;
        }
    }

    public void TriggerPausedState()
    {
        Status = DownloadStatus.Paused;
        IcnStatus.Glyph = "\uE769";
        LblStatus.Text = _translator._("Paused");
        ProgBar.IsIndeterminate = false;
        FntPauseResume.Glyph = "\uE768";
        SetActionLabel(BtnPauseResume, _translator._("Resume"));
        _isPaused = true;
    }

    public void TriggerProgressState(DownloadProgressChangedEventArgs args)
    {
        IcnStatus.Glyph = "\uE896";
        if (args.LogChunk.Length > 0)
        {
            _log += $"{args.LogChunk.ToString()}\n";
        }
        if (double.IsNaN(args.Progress))
        {
            LblStatus.Text = _translator._("Processing");
            ProgBar.Value = 0.0;
            ProgBar.IsIndeterminate = true;
        }
        else if (double.IsNegativeInfinity(args.Progress))
        {
            LblStatus.Text = _translator._("Sleeping for {0}", TimeSpan.FromSeconds(args.Speed).ToString("g"));
            ProgBar.Value = 0.0;
            ProgBar.IsIndeterminate = true;
        }
        else
        {
            LblStatus.Text = _translator._("{0}% Complete • {1} • {2} Remaining",
                Math.Round(args.Progress * 100, 2),
                args.Speed > 0 ? args.SpeedString : _translator._("Unknown"),
                args.Eta > 0 ? args.EtaString : _translator._("Unknown"));
            ProgBar.Value = args.Progress;
            ProgBar.IsIndeterminate = false;
        }
        LblLog.Text = _log;
        ScrollLog.ChangeView(null, ScrollLog.ScrollableHeight, null);
    }

    public void TriggerResumedState()
    {
        Status = DownloadStatus.Running;
        FntPauseResume.Glyph = "\uE769";
        SetActionLabel(BtnPauseResume, _translator._("Pause"));
        _isPaused = false;
    }

    public void TriggerStartedFromQueueState()
    {
        Status = DownloadStatus.Running;
        LblStatus.Text = _translator._("Running");
        ProgBar.Value = 0.0;
        ProgBar.IsIndeterminate = true;
    }

    public void TriggerStoppedState()
    {
        Status = DownloadStatus.Stopped;
        IcnStatus.Glyph = "\uE783";
        LblStatus.Text = _translator._("Stopped");
        ProgBar.Value = 1.0;
        ProgBar.IsIndeterminate = false;
        ViewStackButtons.SelectedIndex = (int)ButtonsPage.Error;
    }

    private async void CopyFile(object sender, RoutedEventArgs e)
    {
        if (!BtnCopyFile.IsEnabled)
        {
            return;
        }
        BtnCopyFile.IsEnabled = false;
        string resultText;
        string resultGlyph;
        try
        {
            if (!File.Exists(_path))
            {
                throw new FileNotFoundException(null, _path);
            }
            var file = await StorageFile.GetFileFromPathAsync(_path);
            var package = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            package.SetStorageItems([file]);
            Clipboard.SetContent(package);
            // Keep the file on the clipboard after BearDL closes.
            Clipboard.Flush();
            resultText = _translator._("Copied");
            resultGlyph = "\uE73E";
        }
        catch (FileNotFoundException)
        {
            resultText = _translator._("File not found");
            resultGlyph = "\uE783";
        }
        catch
        {
            resultText = _translator._("Error");
            resultGlyph = "\uE783";
        }
        try
        {
            if (IsLoaded)
            {
                AutomationProperties.SetName(BtnCopyFile, resultText);
                CopyFileToolTip.Content = resultText;
                CopyFileToolTip.IsOpen = true;
                FntCopyFile.Glyph = resultGlyph;
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
        finally
        {
            CopyFileToolTip.IsOpen = false;
            CopyFileToolTip.Content = _translator._("Copy the file to paste it into another app");
            AutomationProperties.SetName(BtnCopyFile, _translator._("Copy"));
            FntCopyFile.Glyph = "\uE8C8";
            BtnCopyFile.IsEnabled = true;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => CopyFileToolTip.IsOpen = false;

    private async void OpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo()
            {
                FileName = Path.GetDirectoryName(_path)!,
                UseShellExecute = true
            });
        }
        catch
        {
            await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(_path)!));
        }
    }

    private void PauseResume(object sender, RoutedEventArgs e)
    {
        if (_isPaused)
        {
            ResumeRequested?.Invoke(this, _id);
        }
        else
        {
            PauseRequested?.Invoke(this, _id);
        }
    }

    private async void Play(object sender, RoutedEventArgs e)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo()
            {
                FileName = _path,
                UseShellExecute = true
            });
        }
        catch
        {
            await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(_path));
        }
    }

    private void Retry(object sender, RoutedEventArgs e) => RetryRequested?.Invoke(this, _id);

    private void ShowLog(object sender, RoutedEventArgs e) => GridLog.Visibility = BtnShowLog.IsChecked ? Visibility.Visible : Visibility.Collapsed;

    private void Stop(object sender, RoutedEventArgs e) => StopRequested?.Invoke(this, _id);
}

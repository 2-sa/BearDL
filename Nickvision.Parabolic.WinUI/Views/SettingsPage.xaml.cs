using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.WinUI.Helpers;
using Nickvision.Parabolic.Shared.Controllers;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.WinUI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace Nickvision.Parabolic.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    private readonly PreferencesViewController _controller;
    private readonly ITranslationService _translationService;
    private readonly DispatcherQueueTimer _textDebounceTimer;
    private bool _constructing;
    private bool _pendingProxyTextSave;
    private bool _pendingDiscoveryArgsTextSave;
    private bool _pendingDownloadArgsTextSave;

    public WindowId? WindowId { get; set; }

    public SettingsPage(PreferencesViewController controller, ITranslationService translationService)
    {
        InitializeComponent();
        LocalizationHelper.Apply(this, translationService);
        ToggleSwitchHelper.Apply(FlowDirection,
            TglPreviewUpdates,
            TglPreventSuspend,
            TglFastDownload,
            TglWarnBeforeDuplicateDownload,
            TglOverwriteFiles,
            TglIncludeMediaId,
            TglIncludeAutoSubtitles,
            TglIncludeSuperResolutions,
            TglUsePartFiles,
            TglUseSponsorBlock,
            TglLimitSpeed,
            TglUseAria,
            TglTranslateMetadataAndChapters,
            TglEmbedMetadata,
            TglRemoveSourceData,
            TglEmbedThumbnails,
            TglCropAudioThumbnails,
            TglEmbedChapters,
            TglEmbedSubtitles);
        ToggleSwitchHelper.ApplyExpanders(FlowDirection,
            RowUseAria,
            RowEmbedMetadata,
            RowEmbedThumbnails);
        _controller = controller;
        _translationService = translationService;
        _constructing = true;
        _textDebounceTimer = DispatcherQueue.CreateTimer();
        _textDebounceTimer.Interval = TimeSpan.FromMilliseconds(350);
        _textDebounceTimer.IsRepeating = false;
        _textDebounceTimer.Tick += (_, _) => FlushPendingTextChanges();
        Unloaded += (_, _) =>
        {
            _textDebounceTimer.Stop();
            FlushPendingTextChanges();
        };
        // Translations
        NavigationView.PaneTitle = _translationService._("Settings");
        LblSettingsTitle.Text = _translationService._("Settings");
        LblSettingsSection.Text = _translationService._("User Interface");
        NavUserInterface.Content = _translationService._("User Interface");
        NavDownloads.Content = _translationService._("Downloads");
        NavDownloader.Content = _translationService._("Downloader");
        NavConverter.Content = _translationService._("Converter");
        RowTheme.Header = _translationService._("Theme");
        CmbTheme.ItemsSource = _controller.Themes.ToBindableSelectonItems();
        RowTranslationLanguage.Header = _translationService._("Translation Language");
        RowTranslationLanguage.Description = _translationService._("An application restart is required for a change to take effect");
        CmbTranslationLanguage.ItemsSource = _controller.AvailableTranslationLanguages.ToBindableSelectonItems();
        RowPreviewUpdates.Header = _translationService._("Receive Preview Updates");
        RowPreviewUpdates.Description = _translationService._("Update BearDL and dependencies, such as yt-dlp, to beta versions");
        AutomationProperties.SetName(TglPreviewUpdates, (string)RowPreviewUpdates.Header);
        RowPreventSuspend.Header = _translationService._("Prevent Suspend");
        RowPreventSuspend.Description = _translationService._("Prevent the computer from sleeping while downloads are running");
        AutomationProperties.SetName(TglPreventSuspend, (string)RowPreventSuspend.Header);
        RowHistoryLength.Header = _translationService._("Download History Length");
        RowHistoryLength.Description = _translationService._("The amount of time to keep past downloads in the app's history");
        CmbHistoryLength.ItemsSource = _controller.HistoryLengths.ToBindableSelectonItems();
        RowActiveDownloads.Header = _translationService._("Max Number of Active Downloads");
        RowFastDownload.Header = _translationService._("Fast Download");
        RowFastDownload.Description = _translationService._("Download links and entire playlists at the best quality to the last save folder, without configuration dialogs.");
        AutomationProperties.SetName(TglFastDownload, (string)RowFastDownload.Header);
        RowWarnBeforeDuplicateDownload.Header = _translationService._("Warn Before Duplicate Downloads");
        RowWarnBeforeDuplicateDownload.Description = _translationService._("Ask before adding a link that is already downloading or has a saved file.");
        AutomationProperties.SetName(TglWarnBeforeDuplicateDownload, (string)RowWarnBeforeDuplicateDownload.Header);
        RowOverwriteFiles.Header = _translationService._("Overwrite Existing Files");
        AutomationProperties.SetName(TglOverwriteFiles, (string)RowOverwriteFiles.Header);
        RowIncludeMediaId.Header = _translationService._("Include Media Id in Title");
        RowIncludeMediaId.Description = _translationService._("Add the media's id to its default title");
        AutomationProperties.SetName(TglIncludeMediaId, (string)RowIncludeMediaId.Header);
        RowIncludeAutoSubtitles.Header = _translationService._("Include Auto-Generated Subtitles");
        RowIncludeAutoSubtitles.Description = _translationService._("Show auto-generated subtitles to download in addition to available subtitles");
        AutomationProperties.SetName(TglIncludeAutoSubtitles, (string)RowIncludeAutoSubtitles.Header);
        RowIncludeSuperResolutions.Header = _translationService._("Include Super Resolution Formats");
        RowIncludeSuperResolutions.Description = _translationService._("Show super (AI-scaled) resolution formats to download in addition to regular formats");
        AutomationProperties.SetName(TglIncludeSuperResolutions, (string)RowIncludeSuperResolutions.Header);
        RowPreferredVideoCodec.Header = _translationService._("Preferred Video Codec");
        RowPreferredVideoCodec.Description = _translationService._("Prefer this codec when parsing video formats to show available to download");
        CmbPreferredVideoCodec.ItemsSource = _controller.VideoCodecs.ToBindableSelectonItems();
        RowPreferredAudioCodec.Header = _translationService._("Preferred Audio Codec");
        RowPreferredAudioCodec.Description = _translationService._("Prefer this codec when parsing audio formats to show available to download");
        CmbPreferredAudioCodec.ItemsSource = _controller.AudioCodecs.ToBindableSelectonItems();
        RowPreferredSubtitleFormat.Header = _translationService._("Preferred Subtitle Format");
        RowPreferredSubtitleFormat.Description = _translationService._("Prefer this subtitle file format when downloading");
        CmbPreferredSubtitleFormat.ItemsSource = _controller.SubtitleFormats.ToBindableSelectonItems();
        RowPreferredFrameRate.Header = _translationService._("Preferred Frame Rate");
        RowPreferredFrameRate.Description = _translationService._("Prefer this frame rate when parsing video formats to show available to download");
        CmbPreferredFrameRate.ItemsSource = _controller.FrameRates.ToBindableSelectonItems();
        RowUsePartFiles.Header = _translationService._("Use Part Files");
        RowUsePartFiles.Description = _translationService._("Download media in separate .part files instead of directly into the output file");
        AutomationProperties.SetName(TglUsePartFiles, (string)RowUsePartFiles.Header);
        RowUseSponsorBlock.Header = _translationService._("Use SponsorBlock for YouTube");
        RowUseSponsorBlock.Description = _translationService._("Try to remove sponsored segments from videos");
        AutomationProperties.SetName(TglUseSponsorBlock, (string)RowUseSponsorBlock.Header);
        RowLimitSpeed.Header = _translationService._("Limit Download Speed");
        AutomationProperties.SetName(TglLimitSpeed, (string)RowLimitSpeed.Header);
        RowSpeedLimit.Header = _translationService._("Speed Limit");
        RowProxyUrl.Header = _translationService._("Proxy URL");
        TxtProxyUrl.PlaceholderText = _translationService._("Enter proxy url here");
        RowCookiesFile.Header = _translationService._("Cookies from File");
        RowCookiesFile.Description = _translationService._("Upload a txt cookies file from unlisted browsers");
        LblCookiesFile.Text = _translationService._("No file selected");
        ToolTipService.SetToolTip(BtnClearCookiesFile, _translationService._("Clear Cookies File"));
        ToolTipService.SetToolTip(BtnSelectCookiesFile, _translationService._("Select Cookies File"));
        RowCookiesBrowser.Header = _translationService._("Cookies from Browser");
        CmbCookiesBrowser.ItemsSource = _controller.Browsers.ToBindableSelectonItems();
        LblAria.Text = _translationService._("aria2c");
        RowUseAria.Header = _translationService._("Use aria2c");
        RowUseAria.Description = _translationService._("An alternative downloader that may be faster in some regions compared to yt-dlp's native downloader");
        AutomationProperties.SetName(TglUseAria, (string)RowUseAria.Header);
        RowMaxConnectionsPerServer.Header = _translationService._("Max Connections Per Server");
        RowMaxConnectionsPerServer.Description = _translationService._("Corresponds to -x option");
        RowMinimumSplitSize.Header = _translationService._("Minimum Split Size (MiB)");
        RowMinimumSplitSize.Description = _translationService._("Corresponds to -k option");
        LblAdvanced.Text = _translationService._("Advanced");
        RowYtdlpDiscoveryArgs.Header = _translationService._("yt-dlp Discovery Arguments");
        RowYtdlpDiscoveryArgs.Description = _translationService._("Extra arguments to pass to yt-dlp when discovering media");
        TxtYtdlpDiscoveryArgs.PlaceholderText = _translationService._("Enter args here");
        RowYtdlpDownloadArgs.Header = _translationService._("yt-dlp Download Arguments");
        RowYtdlpDownloadArgs.Description = _translationService._("Extra arguments to pass to yt-dlp when downloading media");
        TxtYtdlpDownloadArgs.PlaceholderText = _translationService._("Enter args here");
        RowTranslateMetadataAndChapters.Header = _translationService._("Translate Metadata and Chapters");
        RowTranslateMetadataAndChapters.Description = _translationService._("Automatically translate embedded metadata and chapters to the app's language on supported sites and media");
        AutomationProperties.SetName(TglTranslateMetadataAndChapters, (string)RowTranslateMetadataAndChapters.Header);
        RowEmbedMetadata.Header = _translationService._("Embed Metadata");
        AutomationProperties.SetName(TglEmbedMetadata, (string)RowEmbedMetadata.Header);
        RowRemoveSourceData.Header = _translationService._("Remove Source Data");
        RowRemoveSourceData.Description = _translationService._("Clear metadata fields containing identifying download information");
        AutomationProperties.SetName(TglRemoveSourceData, (string)RowRemoveSourceData.Header);
        RowEmbedThumbnails.Header = _translationService._("Embed Thumbnails");
        RowEmbedThumbnails.Description = _translationService._("Embed thumbnails in supported file types without saving separate image files");
        AutomationProperties.SetName(TglEmbedThumbnails, (string)RowEmbedThumbnails.Header);
        RowCropAudioThumbnails.Header = _translationService._("Crop Audio Thumbnails");
        RowCropAudioThumbnails.Description = _translationService._("Crop thumbnails of audio files to squares");
        AutomationProperties.SetName(TglCropAudioThumbnails, (string)RowCropAudioThumbnails.Header);
        RowEmbedChapters.Header = _translationService._("Embed Chapters");
        AutomationProperties.SetName(TglEmbedChapters, (string)RowEmbedChapters.Header);
        RowEmbedSubtitles.Header = _translationService._("Embed Subtitles");
        RowEmbedSubtitles.Description = _translationService._("If disabled or if embedding is not supported, downloaded subtitles will be saved to separate files");
        AutomationProperties.SetName(TglEmbedSubtitles, (string)RowEmbedSubtitles.Header);
        RowFfmpegThreads.Header = _translationService._("FFmpeg Threads");
        RowFfmpegThreads.Description = _translationService._("Limit the number of threads used by ffmpeg");
        NumFfmpegThreads.Maximum = Environment.ProcessorCount;
        LblPostProcessorArgments.Text = _translationService._("Post-Processor Arguments");
        LblAddPostProcessorArgument.Text = _translationService._("Add");
        DlgPostprocessingArgument.Title = _translationService._("Argument");
        DlgPostprocessingArgument.CloseButtonText = _translationService._("Cancel");
        TxtPostprocessingArgumentName.Header = _translationService._("Name");
        TxtPostprocessingArgumentName.PlaceholderText = _translationService._("Enter name here");
        CmbPostprocessingArgumentPostProcessor.Header = _translationService._("Post-Processor");
        CmbPostprocessingArgumentExecutable.Header = _translationService._("Executable");
        LblPostprocessingArgumentArgs.Text = _translationService._("Args");
        TxtPostprocessingArgumentArgs.PlaceholderText = _translationService._("Enter args here");
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        CmbTheme.SelectSelectionItem();
        GlassThemeHelper.AttachSettingsCard(PanelUserInterface, RowTheme, CmbTheme, _controller, _translationService);
        CmbTranslationLanguage.SelectSelectionItem();
        TglPreviewUpdates.IsOn = _controller.AllowPreviewUpdates;
        TglFastDownload.IsOn = _controller.FastDownload;
        TglWarnBeforeDuplicateDownload.IsOn = _controller.WarnBeforeDuplicateDownload;
        TglPreventSuspend.IsOn = _controller.PreventSuspend;
        CmbHistoryLength.SelectSelectionItem();
        NumActiveDownloads.Value = _controller.MaxNumberOfActiveDownloads;
        TglOverwriteFiles.IsOn = _controller.OverwriteExistingFiles;
        TglIncludeMediaId.IsOn = _controller.IncludeMediaIdInTitle;
        TglIncludeAutoSubtitles.IsOn = _controller.IncludeAutoGeneratedSubtitles;
        TglIncludeSuperResolutions.IsOn = _controller.IncludeSuperResolutions;
        CmbPreferredVideoCodec.SelectSelectionItem();
        CmbPreferredAudioCodec.SelectSelectionItem();
        CmbPreferredSubtitleFormat.SelectSelectionItem();
        CmbPreferredFrameRate.SelectSelectionItem();
        TglUsePartFiles.IsOn = _controller.UsePartFiles;
        TglUseSponsorBlock.IsOn = _controller.YouTubeSponsorBlock;
        TglLimitSpeed.IsOn = _controller.SpeedLimit.HasValue;
        NumSpeedLimit.Value = _controller.SpeedLimit ?? 1024;
        TxtProxyUrl.Text = _controller.ProxyUrl;
        LblCookiesFile.Text = !File.Exists(_controller.CookiesPath) ? _translationService._("No file selected") : _controller.CookiesPath;
        CmbCookiesBrowser.SelectSelectionItem();
        TglUseAria.IsOn = _controller.UseAria;
        NumMaxConnectionsPerServer.Value = _controller.AriaMaxConnectionsPerServer;
        NumMinimumSplitSize.Value = _controller.AriaMinSplitSize;
        TxtYtdlpDiscoveryArgs.Text = _controller.YtdlpDiscoveryArgs;
        TxtYtdlpDownloadArgs.Text = _controller.YtdlpDownloadArgs;
        TglTranslateMetadataAndChapters.IsOn = _controller.TranslateMetadataAndChapters;
        TglEmbedMetadata.IsOn = _controller.EmbedMetadata;
        TglRemoveSourceData.IsOn = _controller.RemoveSourceData;
        TglEmbedThumbnails.IsOn = _controller.EmbedThumbnails;
        TglCropAudioThumbnails.IsOn = _controller.CropAudioThumbnails;
        TglEmbedChapters.IsOn = _controller.EmbedChapters;
        TglEmbedSubtitles.IsOn = _controller.EmbedSubtitles;
        NumFfmpegThreads.Value = _controller.PostprocessingThreads;
        ListPostProcessorArguments.ItemsSource = _controller.PostprocessingArguments.ToBindablePostProcessorArguments();
        CmbPostprocessingArgumentPostProcessor.ItemsSource = _controller.PostProcessors.ToBindableSelectonItems();
        CmbPostprocessingArgumentExecutable.ItemsSource = _controller.Executables.ToBindableSelectonItems();
        _constructing = false;
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag || !int.TryParse(tag, out var index))
        {
            return;
        }
        // NavigationView can change selection while measuring its top menu.
        DispatcherQueue.TryEnqueue(() =>
        {
            ViewStack.SelectedIndex = index;
            LblSettingsSection.Text = item.Content as string ?? string.Empty;
        });
    }

    private async void AddPostprocessingArgument(object? sender, RoutedEventArgs e)
    {
        TxtPostprocessingArgumentName.IsReadOnly = false;
        TxtPostprocessingArgumentName.Text = string.Empty;
        CmbPostprocessingArgumentPostProcessor.SelectSelectionItem();
        CmbPostprocessingArgumentExecutable.SelectSelectionItem();
        TxtPostprocessingArgumentArgs.Text = string.Empty;
        DlgPostprocessingArgument.PrimaryButtonText = _translationService._("Add");
        DlgPostprocessingArgument.FlowDirection = FlowDirection;
        DlgPostprocessingArgument.XamlRoot = XamlRoot;
        DlgPostprocessingArgument.RequestedTheme = ActualTheme;
        string? error = null;
        do
        {
            error = null;
            if ((await DialogQueueHelper.ShowQueuedAsync(DlgPostprocessingArgument, XamlRoot)) == ContentDialogResult.Primary)
            {
                error = await _controller.AddPostprocessingArgumentAsync(TxtPostprocessingArgumentName.Text,
                    (CmbPostprocessingArgumentPostProcessor.SelectedItem as BindableSelectionItem)!.ToSelectionItem<PostProcessor>()!,
                    (CmbPostprocessingArgumentExecutable.SelectedItem as BindableSelectionItem)!.ToSelectionItem<Executable>()!,
                    TxtPostprocessingArgumentArgs.Text);
                if (error is not null)
                {
                    var errorDialog = new ContentDialog()
                    {
                        Title = _translationService._("Error"),
                        Content = error,
                        CloseButtonText = _translationService._("OK"),
                        DefaultButton = ContentDialogButton.Close,
                        FlowDirection = this.FlowDirection,
                        XamlRoot = XamlRoot,
                        RequestedTheme = ActualTheme
                    };
                    await DialogQueueHelper.ShowQueuedAsync(errorDialog, XamlRoot);
                }
            }
        } while (error is not null);
    }

    private void ClearCookiesFile(object? sender, RoutedEventArgs e)
    {
        LblCookiesFile.Text = _translationService._("No file selected");
        if (!string.IsNullOrEmpty(_controller.CookiesPath))
        {
            _controller.CookiesPath = string.Empty;
        }
    }

    private async void DeletePostprocessingArgument(object? sender, RoutedEventArgs e)
    {
        var tag = ((sender as Button)!.Tag as string)!;
        if (await DialogQueueHelper.ShowConfirmAsync(
                XamlRoot,
                _translationService._("Delete Argument?"),
                _translationService._("Are you sure you want to delete this post-processor argument? This action is irreversible"),
                _translationService._("Yes"),
                _translationService._("No"),
                FlowDirection,
                ActualTheme))
        {
            await _controller.DeletePostprocessingArgumentAsync(tag);
        }
    }

    private async void EditPostprocessingArgument(object? sender, RoutedEventArgs e)
    {
        var tag = ((sender as Button)!.Tag as string)!;
        var argument = _controller.PostprocessingArguments.First(x => x.Name == tag);
        TxtPostprocessingArgumentName.Text = tag;
        TxtPostprocessingArgumentName.IsReadOnly = true;
        CmbPostprocessingArgumentPostProcessor.SelectedItem = (CmbPostprocessingArgumentPostProcessor.ItemsSource as IEnumerable<BindableSelectionItem>)
            ?.FirstOrDefault(x => x.ToSelectionItem<PostProcessor>()?.Value == argument.PostProcessor);
        CmbPostprocessingArgumentExecutable.SelectedItem = (CmbPostprocessingArgumentExecutable.ItemsSource as IEnumerable<BindableSelectionItem>)
            ?.FirstOrDefault(x => x.ToSelectionItem<Executable>()?.Value == argument.Executable);
        TxtPostprocessingArgumentArgs.Text = argument.Args;
        DlgPostprocessingArgument.PrimaryButtonText = _translationService._("Update");
        DlgPostprocessingArgument.FlowDirection = FlowDirection;
        DlgPostprocessingArgument.XamlRoot = XamlRoot;
        DlgPostprocessingArgument.RequestedTheme = ActualTheme;
        string? error = null;
        do
        {
            error = null;
            if ((await DialogQueueHelper.ShowQueuedAsync(DlgPostprocessingArgument, XamlRoot)) == ContentDialogResult.Primary)
            {
                error = await _controller.UpdatePostprocessingArgumentAsync(TxtPostprocessingArgumentName.Text,
                    (CmbPostprocessingArgumentPostProcessor.SelectedItem as BindableSelectionItem)!.ToSelectionItem<PostProcessor>()!,
                    (CmbPostprocessingArgumentExecutable.SelectedItem as BindableSelectionItem)!.ToSelectionItem<Executable>()!,
                    TxtPostprocessingArgumentArgs.Text);
                if (error is not null)
                {
                    var errorDialog = new ContentDialog()
                    {
                        Title = _translationService._("Error"),
                        Content = error,
                        CloseButtonText = _translationService._("OK"),
                        DefaultButton = ContentDialogButton.Close,
                        FlowDirection = this.FlowDirection,
                        XamlRoot = XamlRoot,
                        RequestedTheme = ActualTheme
                    };
                    await DialogQueueHelper.ShowQueuedAsync(errorDialog, XamlRoot);
                }
            }
        } while (error is not null);
    }

    private async void SelectCookiesFile(object? sender, RoutedEventArgs e)
    {
        var filePicker = new FileOpenPicker(WindowId!.Value)
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            FileTypeFilter = { ".txt" }
        };
        var file = await filePicker.PickSingleFileAsync();
        if (file is not null)
        {
            LblCookiesFile.Text = file.Path;
            if (_controller.CookiesPath != file.Path)
            {
                _controller.CookiesPath = file.Path;
            }
        }
    }

    private void Cmb_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_constructing || sender is not ComboBox comboBox || comboBox.SelectedItem is not BindableSelectionItem selected)
        {
            return;
        }
        if (ReferenceEquals(comboBox, CmbTheme))
        {
            _controller.Theme = selected.ToSelectionItem<Theme>()!;
        }
        else if (ReferenceEquals(comboBox, CmbTranslationLanguage))
        {
            _controller.TranslationLanguage = selected.ToSelectionItem<string>()!;
        }
        else if (ReferenceEquals(comboBox, CmbHistoryLength))
        {
            _controller.HistoryLength = selected.ToSelectionItem<HistoryLength>()!;
        }
        else if (ReferenceEquals(comboBox, CmbPreferredVideoCodec))
        {
            _controller.PreferredVideoCodec = selected.ToSelectionItem<VideoCodec>()!;
        }
        else if (ReferenceEquals(comboBox, CmbPreferredAudioCodec))
        {
            _controller.PreferredAudioCodec = selected.ToSelectionItem<AudioCodec>()!;
        }
        else if (ReferenceEquals(comboBox, CmbPreferredSubtitleFormat))
        {
            _controller.PreferredSubtitleFormat = selected.ToSelectionItem<SubtitleFormat>()!;
        }
        else if (ReferenceEquals(comboBox, CmbPreferredFrameRate))
        {
            _controller.PreferredFrameRate = selected.ToSelectionItem<FrameRate>()!;
        }
        else if (ReferenceEquals(comboBox, CmbCookiesBrowser))
        {
            _controller.CookiesBrowser = selected.ToSelectionItem<Browser>()!;
        }
    }

    private void Num_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_constructing || double.IsNaN(sender.Value))
        {
            return;
        }
        if (ReferenceEquals(sender, NumActiveDownloads))
        {
            var val = (int)Math.Clamp(Math.Round(sender.Value), 1, 10);
            if (_controller.MaxNumberOfActiveDownloads != val)
            {
                _controller.MaxNumberOfActiveDownloads = val;
            }
        }
        else if (ReferenceEquals(sender, NumSpeedLimit))
        {
            if (TglLimitSpeed.IsOn)
            {
                var val = (int)Math.Clamp(Math.Round(sender.Value), 512, 10240);
                if (_controller.SpeedLimit != val)
                {
                    _controller.SpeedLimit = val;
                }
            }
        }
        else if (ReferenceEquals(sender, NumMaxConnectionsPerServer))
        {
            var val = (int)Math.Clamp(Math.Round(sender.Value), 1, 16);
            if (_controller.AriaMaxConnectionsPerServer != val)
            {
                _controller.AriaMaxConnectionsPerServer = val;
            }
        }
        else if (ReferenceEquals(sender, NumMinimumSplitSize))
        {
            var val = (int)Math.Clamp(Math.Round(sender.Value), 1, 1024);
            if (_controller.AriaMinSplitSize != val)
            {
                _controller.AriaMinSplitSize = val;
            }
        }
        else if (ReferenceEquals(sender, NumFfmpegThreads))
        {
            var val = (int)Math.Clamp(Math.Round(sender.Value), 1, Environment.ProcessorCount);
            if (_controller.PostprocessingThreads != val)
            {
                _controller.PostprocessingThreads = val;
            }
        }
    }

    private void Tgl_Toggled(object? sender, RoutedEventArgs e)
    {
        if (_constructing || sender is not ToggleSwitch toggle)
        {
            return;
        }
        if (ReferenceEquals(toggle, TglPreviewUpdates) && _controller.AllowPreviewUpdates != toggle.IsOn)
        {
            _controller.AllowPreviewUpdates = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglFastDownload) && _controller.FastDownload != toggle.IsOn)
        {
            _controller.FastDownload = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglWarnBeforeDuplicateDownload) && _controller.WarnBeforeDuplicateDownload != toggle.IsOn)
        {
            _controller.WarnBeforeDuplicateDownload = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglPreventSuspend) && _controller.PreventSuspend != toggle.IsOn)
        {
            _controller.PreventSuspend = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglOverwriteFiles) && _controller.OverwriteExistingFiles != toggle.IsOn)
        {
            _controller.OverwriteExistingFiles = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglIncludeMediaId) && _controller.IncludeMediaIdInTitle != toggle.IsOn)
        {
            _controller.IncludeMediaIdInTitle = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglIncludeAutoSubtitles) && _controller.IncludeAutoGeneratedSubtitles != toggle.IsOn)
        {
            _controller.IncludeAutoGeneratedSubtitles = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglIncludeSuperResolutions) && _controller.IncludeSuperResolutions != toggle.IsOn)
        {
            _controller.IncludeSuperResolutions = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglUsePartFiles) && _controller.UsePartFiles != toggle.IsOn)
        {
            _controller.UsePartFiles = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglUseSponsorBlock) && _controller.YouTubeSponsorBlock != toggle.IsOn)
        {
            _controller.YouTubeSponsorBlock = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglLimitSpeed))
        {
            int? desired = toggle.IsOn
                ? (double.IsNaN(NumSpeedLimit.Value) ? (_controller.SpeedLimit ?? 1024) : (int)Math.Clamp(Math.Round(NumSpeedLimit.Value), 512, 10240))
                : null;
            if (_controller.SpeedLimit != desired)
            {
                _controller.SpeedLimit = desired;
            }
        }
        else if (ReferenceEquals(toggle, TglUseAria) && _controller.UseAria != toggle.IsOn)
        {
            _controller.UseAria = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglTranslateMetadataAndChapters) && _controller.TranslateMetadataAndChapters != toggle.IsOn)
        {
            _controller.TranslateMetadataAndChapters = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglEmbedMetadata) && _controller.EmbedMetadata != toggle.IsOn)
        {
            _controller.EmbedMetadata = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglRemoveSourceData) && _controller.RemoveSourceData != toggle.IsOn)
        {
            _controller.RemoveSourceData = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglEmbedThumbnails) && _controller.EmbedThumbnails != toggle.IsOn)
        {
            _controller.EmbedThumbnails = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglCropAudioThumbnails) && _controller.CropAudioThumbnails != toggle.IsOn)
        {
            _controller.CropAudioThumbnails = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglEmbedChapters) && _controller.EmbedChapters != toggle.IsOn)
        {
            _controller.EmbedChapters = toggle.IsOn;
        }
        else if (ReferenceEquals(toggle, TglEmbedSubtitles) && _controller.EmbedSubtitles != toggle.IsOn)
        {
            _controller.EmbedSubtitles = toggle.IsOn;
        }
    }

    private void Txt_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_constructing)
        {
            return;
        }
        if (ReferenceEquals(sender, TxtProxyUrl))
        {
            _pendingProxyTextSave = true;
        }
        else if (ReferenceEquals(sender, TxtYtdlpDiscoveryArgs))
        {
            _pendingDiscoveryArgsTextSave = true;
        }
        else if (ReferenceEquals(sender, TxtYtdlpDownloadArgs))
        {
            _pendingDownloadArgsTextSave = true;
        }
        _textDebounceTimer.Stop();
        _textDebounceTimer.Start();
    }

    private void FlushPendingTextChanges()
    {
        if (_constructing)
        {
            return;
        }
        if (_pendingProxyTextSave)
        {
            _pendingProxyTextSave = false;
            if (_controller.ProxyUrl != TxtProxyUrl.Text)
            {
                _controller.ProxyUrl = TxtProxyUrl.Text;
            }
        }
        if (_pendingDiscoveryArgsTextSave)
        {
            _pendingDiscoveryArgsTextSave = false;
            if (_controller.YtdlpDiscoveryArgs != TxtYtdlpDiscoveryArgs.Text)
            {
                _controller.YtdlpDiscoveryArgs = TxtYtdlpDiscoveryArgs.Text;
            }
        }
        if (_pendingDownloadArgsTextSave)
        {
            _pendingDownloadArgsTextSave = false;
            if (_controller.YtdlpDownloadArgs != TxtYtdlpDownloadArgs.Text)
            {
                _controller.YtdlpDownloadArgs = TxtYtdlpDownloadArgs.Text;
            }
        }
    }
}

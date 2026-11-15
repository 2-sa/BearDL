using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nickvision.Desktop.Globalization;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Nickvision.Parabolic.WinUI.Helpers;

public static class DialogQueueHelper
{
    private static readonly ConditionalWeakTable<XamlRoot, SemaphoreSlim> s_gates = new();
    private static readonly ConditionalWeakTable<XamlRoot, DispatcherQueue> s_dispatchers = new();

    public static void RegisterDispatcher(XamlRoot? xamlRoot, DispatcherQueue? dispatcherQueue)
    {
        if (xamlRoot is null || dispatcherQueue is null)
        {
            return;
        }
        s_dispatchers.AddOrUpdate(xamlRoot, dispatcherQueue);
    }

    private static DispatcherQueue? ResolveDispatcherQueue(XamlRoot xamlRoot)
    {
        var current = DispatcherQueue.GetForCurrentThread();
        if (current is not null)
        {
            s_dispatchers.AddOrUpdate(xamlRoot, current);
            return current;
        }
        return s_dispatchers.TryGetValue(xamlRoot, out var cached) ? cached : null;
    }

    public static ContentDialog? FindOpenDialog(DependencyObject? root)
    {
        if (root is ContentDialog dialog)
        {
            return dialog;
        }
        if (root is null)
        {
            return null;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindOpenDialog(VisualTreeHelper.GetChild(root, i)) is ContentDialog child)
            {
                return child;
            }
        }
        return null;
    }

    public static async Task WaitForOpenDialogsAsync(XamlRoot? xamlRoot, CancellationToken cancellationToken = default)
    {
        if (xamlRoot is null)
        {
            return;
        }
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var openPair = VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot)
                .Select(popup => (Popup: popup, Dialog: FindOpenDialog(popup.Child)))
                .FirstOrDefault(pair => pair.Dialog is not null);
            if (openPair.Dialog is null)
            {
                break;
            }
            var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args) => closed.TrySetResult(true);
            void OnPopupClosed(object? sender, object args) => closed.TrySetResult(true);
            openPair.Dialog.Closed += OnClosed;
            openPair.Popup.Closed += OnPopupClosed;
            try
            {
                if (!openPair.Popup.IsOpen)
                {
                    continue;
                }
                await closed.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                openPair.Dialog.Closed -= OnClosed;
                openPair.Popup.Closed -= OnPopupClosed;
            }
        }
    }

    public static Task<ContentDialogResult> ShowQueuedAsync(ContentDialog dialog, XamlRoot? xamlRoot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        if (xamlRoot is null)
        {
            return Task.FromResult(ContentDialogResult.None);
        }
        return ShowQueuedAsync(async () =>
        {
            dialog.XamlRoot = xamlRoot;
            return await dialog.ShowAsync();
        }, xamlRoot, cancellationToken);
    }

    public static async Task<ContentDialogResult> ShowQueuedAsync(Func<Task<ContentDialogResult>> showDialogAsync, XamlRoot? xamlRoot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(showDialogAsync);
        if (xamlRoot is null)
        {
            return ContentDialogResult.None;
        }
        var dispatcher = ResolveDispatcherQueue(xamlRoot);
        if (dispatcher is null)
        {
            return ContentDialogResult.None;
        }
        if (!dispatcher.HasThreadAccess)
        {
            var tcs = new TaskCompletionSource<ContentDialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    tcs.TrySetResult(await ShowQueuedCoreAsync(showDialogAsync, xamlRoot, cancellationToken));
                }
                catch (OperationCanceledException canceledEx)
                {
                    tcs.TrySetCanceled(canceledEx.CancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
            {
                return ContentDialogResult.None;
            }
            return await tcs.Task;
        }
        return await ShowQueuedCoreAsync(showDialogAsync, xamlRoot, cancellationToken);
    }

    public static async Task RunQueuedAsync(Func<Task> showActionAsync, XamlRoot? xamlRoot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(showActionAsync);
        await ShowQueuedAsync(async () =>
        {
            await showActionAsync();
            return ContentDialogResult.None;
        }, xamlRoot, cancellationToken);
    }

    public static async Task<bool> ShowConfirmAsync(
        XamlRoot? xamlRoot,
        string title,
        string message,
        string primaryButtonText,
        string closeButtonText,
        FlowDirection flowDirection,
        ElementTheme requestedTheme,
        ContentDialogButton defaultButton = ContentDialogButton.Close,
        CancellationToken cancellationToken = default)
    {
        if (xamlRoot is null)
        {
            return false;
        }
        var dispatcher = ResolveDispatcherQueue(xamlRoot);
        if (dispatcher is null)
        {
            return false;
        }
        if (!dispatcher.HasThreadAccess)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    tcs.TrySetResult(await ShowConfirmAsync(xamlRoot, title, message, primaryButtonText, closeButtonText, flowDirection, requestedTheme, defaultButton, cancellationToken));
                }
                catch (OperationCanceledException canceledEx)
                {
                    tcs.TrySetCanceled(canceledEx.CancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
            {
                return false;
            }
            return await tcs.Task;
        }
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = defaultButton,
            FlowDirection = flowDirection,
            RequestedTheme = requestedTheme,
            XamlRoot = xamlRoot
        };
        return await ShowQueuedAsync(dialog, xamlRoot, cancellationToken) == ContentDialogResult.Primary;
    }

    public static async Task<bool> ConfirmDuplicateDownloadAsync(
        XamlRoot? xamlRoot,
        string message,
        ITranslationService translationService,
        FlowDirection flowDirection,
        ElementTheme requestedTheme,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(translationService);
        if (xamlRoot is null)
        {
            return false;
        }
        var dispatcher = ResolveDispatcherQueue(xamlRoot);
        if (dispatcher is null)
        {
            return false;
        }
        if (!dispatcher.HasThreadAccess)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    tcs.TrySetResult(await ConfirmDuplicateDownloadAsync(xamlRoot, message, translationService, flowDirection, requestedTheme, cancellationToken));
                }
                catch (OperationCanceledException canceledEx)
                {
                    tcs.TrySetCanceled(canceledEx.CancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
            {
                return false;
            }
            return await tcs.Task;
        }
        var dialog = new ContentDialog
        {
            Title = translationService._("Duplicate Download"),
            Content = new ScrollViewer
            {
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Thickness(0),
                MaxHeight = 320,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TextBlock
                {
                    Text = message,
                    FlowDirection = flowDirection,
                    TextWrapping = TextWrapping.Wrap
                }
            },
            PrimaryButtonText = translationService._("Download Again"),
            CloseButtonText = translationService._("Cancel"),
            DefaultButton = ContentDialogButton.Close,
            FlowDirection = flowDirection,
            RequestedTheme = requestedTheme,
            XamlRoot = xamlRoot
        };
        return await ShowQueuedAsync(dialog, xamlRoot, cancellationToken) == ContentDialogResult.Primary;
    }

    private static async Task<ContentDialogResult> ShowQueuedCoreAsync(Func<Task<ContentDialogResult>> showDialogAsync, XamlRoot xamlRoot, CancellationToken cancellationToken)
    {
        var gate = s_gates.GetValue(xamlRoot, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await WaitForOpenDialogsAsync(xamlRoot, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return await showDialogAsync();
        }
        finally
        {
            gate.Release();
        }
    }
}

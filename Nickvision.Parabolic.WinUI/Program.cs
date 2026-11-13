using Microsoft.Extensions.Hosting;
using Nickvision.Desktop.WinUI.Helpers;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.WinUI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using WinRT;

namespace Nickvision.Parabolic.WinUI;

public static partial class Program
{
    private static readonly object s_activationLock = new();
    private static readonly Queue<Uri> s_pendingActivations = new();
    private static bool s_pendingShow;
    private static Action<Uri?>? s_activationHandler;

    public static void AttachActivationHandler(Action<Uri?> handler)
    {
        lock (s_activationLock)
        {
            s_activationHandler = handler;
            var hadPendingUrls = s_pendingActivations.Count > 0;
            if (s_pendingShow)
            {
                s_pendingShow = false;
                if (!hadPendingUrls)
                {
                    handler(null);
                }
            }
            while (s_pendingActivations.TryDequeue(out var uri))
            {
                handler(uri);
            }
        }
    }

    private static string GetInstanceKey(string[] args)
    {
        if (!args.Contains("--portable", StringComparer.OrdinalIgnoreCase))
        {
            return "BearDL.Main";
        }
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            return "BearDL.Portable";
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(processPath).ToUpperInvariant())))[..16];
        return $"BearDL.Portable.{hash}";
    }

    private static void InstanceActivated(object? sender, AppActivationArguments args)
    {
        try
        {
            var uri = args.Kind switch
            {
                ExtendedActivationKind.Protocol => LaunchUrlParser.Parse(args.Data.As<IProtocolActivatedEventArgs>().Uri.ToString()),
                ExtendedActivationKind.Launch => LaunchUrlParser.FromCommandLine(args.Data.As<ILaunchActivatedEventArgs>().Arguments),
                _ => null
            };
            lock (s_activationLock)
            {
                if (s_activationHandler is null)
                {
                    if (uri is not null)
                    {
                        s_pendingActivations.Enqueue(uri);
                    }
                    else
                    {
                        s_pendingShow = true;
                    }
                }
                else
                {
                    s_activationHandler(uri);
                }
            }
        }
        catch
        {
        }
    }
    [LibraryImport("Microsoft.ui.xaml.dll", EntryPoint = "XamlCheckProcessRequirements")]
    private static partial void XamlCheckProcessRequirements();

    [STAThread]
    private static void Main(string[] args)
    {
        XamlCheckProcessRequirements();
        ComWrappersSupport.InitializeComWrappers();
        AppInstance.GetCurrent().Activated += InstanceActivated;
        var instance = AppInstance.FindOrRegisterForKey(GetInstanceKey(args));
        if (!instance.IsCurrent)
        {
            try
            {
                var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
                Task.Run(async () => await instance.RedirectActivationToAsync(activation))
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .GetAwaiter()
                    .GetResult();
            }
            catch
            {
            }
            return;
        }
        var builder = Host.CreateApplicationBuilder(args);
        builder.ConfigureParabolic(args);
        builder.ConfigureWinUI<App>();
        builder.Services.AddControls();
        var app = builder.Build();
        app.Run();
    }
}

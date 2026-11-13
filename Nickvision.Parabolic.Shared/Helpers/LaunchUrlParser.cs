using System;
using System.IO;

namespace Nickvision.Parabolic.Shared.Helpers;

public static partial class LaunchUrlParser
{
    public static Uri? Parse(string value)
    {
        value = value.Trim().Trim('"');
        const string prefix = "parabolic://";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest = value[prefix.Length..];
            if (rest.StartsWith("http/", StringComparison.OrdinalIgnoreCase))
            {
                value = "http://" + rest["http/".Length..];
            }
            else if (rest.StartsWith("https/", StringComparison.OrdinalIgnoreCase))
            {
                value = "https://" + rest["https/".Length..];
            }
            else
            {
                value = "https://" + rest;
            }
        }
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri : null;
    }

    public static Uri? FromCommandLine(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }
        foreach (var rawArg in commandLine.SplitCommandLine())
        {
            var arg = rawArg.Trim();
            if (string.IsNullOrEmpty(arg)
                || arg.StartsWith("--", StringComparison.Ordinal)
                || arg.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (Parse(arg) is Uri launchUrl)
            {
                return launchUrl;
            }
            if (arg.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(arg, UriKind.Absolute, out var fileUri)
                && fileUri.IsFile)
            {
                return fileUri;
            }
            if (arg.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var fullPath = Path.GetFullPath(arg);
                    if (Uri.TryCreate(fullPath, UriKind.Absolute, out var txtUri) && txtUri.IsFile)
                    {
                        return txtUri;
                    }
                }
                catch
                {
                    // Ignore invalid file paths
                }
            }
        }
        return null;
    }
}

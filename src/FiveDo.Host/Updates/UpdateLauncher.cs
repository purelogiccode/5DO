using System;
using System.Diagnostics;

namespace FiveDo.Host;

/// <summary>
/// Opens a release or asset page in the user's default browser.
/// </summary>
public static class UpdateLauncher
{
    /// <summary>Opens an HTTP(S) URL with the platform shell.</summary>
    /// <param name="url">Release or download URL.</param>
    /// <returns><see langword="true"/> when the browser was started.</returns>
    public static bool TryOpen(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)))
        {
            return false;
        }

        try
        {
            using Process process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true,
            }) ?? throw new InvalidOperationException("The shell did not start a browser.");
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or PlatformNotSupportedException)
        {
            Serilog.Log.Debug(exception, "The release page could not be opened.");
            return false;
        }
    }
}

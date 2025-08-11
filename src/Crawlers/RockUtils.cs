using System.Text;

namespace Crawlers;

public abstract class RockUtils
{
   
    /// <summary>
    /// Determines whether the current application is running on a Windows operating system.
    /// </summary>
    /// <returns><c>true</c> if the application is running on a Windows operating system; otherwise, <c>false</c>.</returns>
    public static bool IsWindows() => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

    
    /// <summary>
    /// Not necessary for Windows C#, all text is stored as UTF-16
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string Windows1252ToUtf8(string text)
    {
        return text;
    }

    
    /// <summary>
    /// Converts a time duration in milliseconds to a human-readable string representation.
    /// </summary>
    /// <param name="milliseconds">The duration in milliseconds to be converted.</param>
    /// <returns>A string representing the duration in a human-readable format, such as "one second", "two minutes", or "indefinitely".</returns>
    public static string MilliSecondsDeltaToString(long milliseconds)
    {
        var seconds = milliseconds / 1000L;
        switch (seconds)
        {
            case <= 0:
                return "less than a second";
            case < 60 and 1L:
                return "one second";
            case < 60:
                return $"{seconds} seconds";
            case < 3600:
            {
                var minutes = seconds / 60;
                return minutes == 1L ? "one minute" : $"{minutes} minutes";
            }
            case < 86400:
            {
                var hours = seconds / 3600;
                return hours == 1L ? "one hour" : $"{hours} hours";
            }
        }

        var days = seconds / 86400;
        return days switch
        {
            >= 360 => "indefinitely",
            1L => "one day",
            _ => $"{days} days"
        };
    }

    /// <summary>
    /// Creates a new instance of <see cref="HttpClient"/> with custom configurations if required.
    /// </summary>
    /// <param name="allowSelfSignedCertificate">A boolean indicating whether to allow self-signed SSL certificates.</param>
    /// <returns>A configured instance of <see cref="HttpClient"/>. If <paramref name="allowSelfSignedCertificate"/> is true, it bypasses SSL certificate validation.</returns>
    public static HttpClient NewHttpClient(bool allowSelfSignedCertificate)
    {
        if (!allowSelfSignedCertificate) return new HttpClient();
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        return new HttpClient(handler);
    }
    
}

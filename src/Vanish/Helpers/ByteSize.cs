using System.Globalization;

namespace Vanish.Helpers;

/// <summary>Formats byte counts as human-readable strings (KB, MB, GB…).</summary>
public static class ByteSize
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Humanize(long bytes)
    {
        if (bytes <= 0) return "0 B";

        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        // No decimals for bytes/KB, one decimal beyond that.
        string format = unit <= 1 ? "0" : "0.0";
        return string.Create(CultureInfo.InvariantCulture, $"{size.ToString(format, CultureInfo.InvariantCulture)} {Units[unit]}");
    }

    /// <summary>Transfer rate, e.g. "1.4 MB/s".</summary>
    public static string Rate(double bytesPerSecond)
    {
        if (double.IsNaN(bytesPerSecond) || bytesPerSecond < 1) return "0 KB/s";
        if (bytesPerSecond < 1024) return "1 KB/s";
        return Humanize((long)bytesPerSecond) + "/s";
    }
}

using System;

namespace MarkItDownNet.Cli;

public static class Clock
{
    public static DateTime UtcNow => DateTime.UtcNow;
    public static string Iso8601() => UtcNow.ToString("o");
}


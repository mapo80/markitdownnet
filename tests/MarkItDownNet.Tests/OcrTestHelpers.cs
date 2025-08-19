using System;
using System.IO;
using TesseractOCR.InteropDotNet;

namespace MarkItDownNet.Tests;

internal static class OcrTestHelpers
{
    public static void EnsureOcrLibraries()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "x64");
        Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", dir);
        LibraryLoader.Instance.CustomSearchPath = dir;
    }
}

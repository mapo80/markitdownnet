using System;
using TesseractOCR.InteropDotNet;

namespace MarkItDownNet.Tests;

internal static class OcrTestHelpers
{
    public static void EnsureOcrLibraries()
    {
        Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", "/usr/lib/x86_64-linux-gnu");
        LibraryLoader.Instance.CustomSearchPath = "/usr/lib/x86_64-linux-gnu";
    }
}

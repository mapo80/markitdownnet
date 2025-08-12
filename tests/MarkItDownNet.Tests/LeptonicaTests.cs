using System;
using System.Runtime.InteropServices;
using Xunit;

namespace MarkItDownNet.Tests;

public class LeptonicaTests
{
    private const string LeptonicaDll = "libleptonica.so";

    [DllImport(LeptonicaDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pixCreate(int width, int height, int depth);

    [DllImport(LeptonicaDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pixGetWidth(IntPtr pix);

    [DllImport(LeptonicaDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pixGetHeight(IntPtr pix);

    [DllImport(LeptonicaDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pixGetDepth(IntPtr pix);

    [DllImport(LeptonicaDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pixSetPixel(IntPtr pix, int x, int y, uint value);

    [DllImport(LeptonicaDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pixGetPixel(IntPtr pix, int x, int y, out uint value);

    [DllImport(LeptonicaDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void pixDestroy(ref IntPtr pix);

    [Fact]
    public void PixCreate_ShouldReturnCorrectDimensions()
    {
        IntPtr pix = pixCreate(100, 200, 8);
        try
        {
            Assert.NotEqual(IntPtr.Zero, pix);
            Assert.Equal(100, pixGetWidth(pix));
            Assert.Equal(200, pixGetHeight(pix));
            Assert.Equal(8, pixGetDepth(pix));
        }
        finally
        {
            pixDestroy(ref pix);
        }
    }

    [Fact]
    public void PixSetPixel_ShouldRoundTripValue()
    {
        IntPtr pix = pixCreate(1, 1, 8);
        try
        {
            uint expected = 123;
            Assert.Equal(0, pixSetPixel(pix, 0, 0, expected));
            uint actual;
            Assert.Equal(0, pixGetPixel(pix, 0, 0, out actual));
            Assert.Equal(expected, actual);
        }
        finally
        {
            pixDestroy(ref pix);
        }
    }
}

using SkiaSharp;

namespace RapidStructureNet;

/// <summary>
/// Detects page orientation and returns the clockwise rotation angle in degrees
/// required to deskew the image. Implementations may return 0 when the page is
/// already upright.
/// </summary>
public interface IOrientationDetector
{
    float Detect(SKBitmap image);
}

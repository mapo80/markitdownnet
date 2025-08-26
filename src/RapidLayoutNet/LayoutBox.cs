namespace RapidLayoutNet;

public sealed record LayoutBox(LayoutLabel Label, float Score, float X1, float Y1, float X2, float Y2)
{
    public float Width => X2 - X1;
    public float Height => Y2 - Y1;
}

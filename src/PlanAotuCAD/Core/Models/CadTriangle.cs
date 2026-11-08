using Microsoft.Maui.Graphics;

namespace PlanAotuCAD.Core.Models;

public sealed class CadTriangle : CadEntity
{
    public float X1 { get; set; }
    public float Y1 { get; set; }
    public float X2 { get; set; }
    public float Y2 { get; set; }

    public CadTriangle() { }
    public CadTriangle(float x1, float y1, float x2, float y2)
    { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; }

    public PointF A => new(X1, Y1);
    public PointF B => new(X2, Y1);
    public PointF C => new((X1 + X2) / 2f, Y2);

    public override bool HitTest(PointF p, float tolerance)
    {
        if (IsFilled && Inside(p, A, B, C)) return true;
        return Near(p, A, B, tolerance) || Near(p, B, C, tolerance) || Near(p, C, A, tolerance);
    }

    public override void Move(float dx, float dy) { X1 += dx; X2 += dx; Y1 += dy; Y2 += dy; }

    private static bool Inside(PointF p, PointF a, PointF b, PointF c)
    {
        static float Sign(PointF p1, PointF p2, PointF p3) =>
            (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        var d1 = Sign(p, a, b); var d2 = Sign(p, b, c); var d3 = Sign(p, c, a);
        return !(d1 < 0 || d2 < 0 || d3 < 0) || !(d1 > 0 || d2 > 0 || d3 > 0);
    }

    private static bool Near(PointF p, PointF a, PointF b, float t)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var length = dx * dx + dy * dy;
        if (length <= float.Epsilon) return Distance(p, a) <= t;
        var u = Math.Clamp(((p.X-a.X)*dx + (p.Y-a.Y)*dy)/length, 0f, 1f);
        return Distance(p, new PointF(a.X + u*dx, a.Y + u*dy)) <= t;
    }
    private static float Distance(PointF a, PointF b) =>
        MathF.Sqrt((a.X-b.X)*(a.X-b.X) + (a.Y-b.Y)*(a.Y-b.Y));
}
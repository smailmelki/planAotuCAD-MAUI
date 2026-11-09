using Microsoft.Maui.Graphics;

namespace PlanAotuCAD.Core.Models;

public sealed class CadVertex
{
    public float X { get; set; }
    public float Y { get; set; }
    public CadVertex() { }
    public CadVertex(float x, float y) { X = x; Y = y; }
}

public sealed class CadPolyline : CadEntity
{
    public List<CadVertex> Points { get; set; } = new();
    public bool Closed { get; set; }

    public override bool HitTest(PointF point, float tolerance)
    {
        if (Points.Count < 2) return false;
        if (Closed && IsFilled && IsInside(point)) return true;

        for (var i = 1; i < Points.Count; i++)
            if (DistanceToSegment(point, Points[i - 1], Points[i]) <= tolerance) return true;

        return Closed && DistanceToSegment(point, Points[^1], Points[0]) <= tolerance;
    }

    public override void Move(float deltaX, float deltaY)
    {
        foreach (var point in Points) { point.X += deltaX; point.Y += deltaY; }
    }

    private bool IsInside(PointF p)
    {
        var inside = false;
        for (int i = 0, j = Points.Count - 1; i < Points.Count; j = i++)
        {
            var a = Points[i]; var b = Points[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) &&
                p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private static float DistanceToSegment(PointF p, CadVertex a, CadVertex b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var length = dx * dx + dy * dy;
        if (length <= float.Epsilon)
            return MathF.Sqrt((p.X-a.X)*(p.X-a.X) + (p.Y-a.Y)*(p.Y-a.Y));
        var t = Math.Clamp(((p.X-a.X)*dx + (p.Y-a.Y)*dy) / length, 0f, 1f);
        var x = a.X + t * dx; var y = a.Y + t * dy;
        return MathF.Sqrt((p.X-x)*(p.X-x) + (p.Y-y)*(p.Y-y));
    }
}

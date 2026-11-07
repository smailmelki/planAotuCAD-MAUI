using Microsoft.Maui.Graphics;

namespace PlanAotuCAD.Core.Models;

public sealed class CadLine : CadEntity
{
    public float StartX { get; set; }
    public float StartY { get; set; }
    public float EndX { get; set; }
    public float EndY { get; set; }

    public CadLine() { }

    public CadLine(float startX, float startY, float endX, float endY)
    {
        StartX = startX;
        StartY = startY;
        EndX = endX;
        EndY = endY;
    }

    public override bool HitTest(PointF point, float tolerance)
    {
        var start = new PointF(StartX, StartY);
        var end = new PointF(EndX, EndY);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;

        if (lengthSquared <= float.Epsilon)
            return Distance(point, start) <= tolerance;

        var projection = Math.Clamp(
            ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared,
            0f,
            1f);

        var nearest = new PointF(start.X + projection * dx, start.Y + projection * dy);
        return Distance(point, nearest) <= tolerance;
    }

    public override void Move(float deltaX, float deltaY)
    {
        StartX += deltaX;
        StartY += deltaY;
        EndX += deltaX;
        EndY += deltaY;
    }

    private static float Distance(PointF a, PointF b) =>
        MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));
}

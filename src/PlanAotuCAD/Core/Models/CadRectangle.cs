using Microsoft.Maui.Graphics;

namespace PlanAotuCAD.Core.Models;

public sealed class CadRectangle : CadEntity
{
    public float StartX { get; set; }
    public float StartY { get; set; }
    public float EndX { get; set; }
    public float EndY { get; set; }

    public CadRectangle() { }

    public CadRectangle(float startX, float startY, float endX, float endY)
    {
        StartX = startX;
        StartY = startY;
        EndX = endX;
        EndY = endY;
    }

    public float Width => Math.Abs(EndX - StartX);
    public float Height => Math.Abs(EndY - StartY);
    public float Left => Math.Min(StartX, EndX);
    public float Right => Math.Max(StartX, EndX);
    public float Bottom => Math.Min(StartY, EndY);
    public float Top => Math.Max(StartY, EndY);

    public override bool HitTest(PointF point, float tolerance)
    {
        if (point.X < Left - tolerance || point.X > Right + tolerance ||
            point.Y < Bottom - tolerance || point.Y > Top + tolerance)
            return false;

        var inside = point.X >= Left && point.X <= Right &&
                     point.Y >= Bottom && point.Y <= Top;

        if (inside)
            return true;

        return MathF.Abs(point.X - Left) <= tolerance ||
               MathF.Abs(point.X - Right) <= tolerance ||
               MathF.Abs(point.Y - Bottom) <= tolerance ||
               MathF.Abs(point.Y - Top) <= tolerance;
    }

    public override void Move(float deltaX, float deltaY)
    {
        StartX += deltaX;
        StartY += deltaY;
        EndX += deltaX;
        EndY += deltaY;
    }
}

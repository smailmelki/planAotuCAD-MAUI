using Microsoft.Maui.Graphics;

namespace PlanAotuCAD.Core.Models;

public sealed class CadCircle : CadEntity
{
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float Radius { get; set; }

    public CadCircle() { }

    public CadCircle(float centerX, float centerY, float radius)
    {
        CenterX = centerX;
        CenterY = centerY;
        Radius = Math.Max(0, radius);
    }

    public override bool HitTest(PointF point, float tolerance)
    {
        var distance = MathF.Sqrt(
            MathF.Pow(point.X - CenterX, 2) +
            MathF.Pow(point.Y - CenterY, 2));

        return MathF.Abs(distance - Radius) <= tolerance ||
               distance <= tolerance;
    }

    public override void Move(float deltaX, float deltaY)
    {
        CenterX += deltaX;
        CenterY += deltaY;
    }
}

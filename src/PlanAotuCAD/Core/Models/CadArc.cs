using Microsoft.Maui.Graphics;

namespace PlanAotuCAD.Core.Models;

public sealed class CadArc : CadEntity
{
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float Radius { get; set; }
    public float StartAngle { get; set; }
    public float SweepAngle { get; set; } = 180f;

    public CadArc() { }
    public CadArc(float x1, float y1, float x2, float y2)
    {
        CenterX = (x1 + x2) / 2f;
        CenterY = (y1 + y2) / 2f;
        Radius = MathF.Sqrt((x2-x1)*(x2-x1) + (y2-y1)*(y2-y1)) / 2f;
        StartAngle = 0;
        SweepAngle = 180;
    }

    public override bool HitTest(PointF p, float tolerance)
    {
        var dx = p.X - CenterX; var dy = p.Y - CenterY;
        var distance = MathF.Sqrt(dx*dx + dy*dy);
        if (MathF.Abs(distance - Radius) > tolerance) return false;
        var angle = (MathF.Atan2(dy, dx) * 180f / MathF.PI + 360f) % 360f;
        var relative = (angle - StartAngle + 360f) % 360f;
        return relative <= MathF.Abs(SweepAngle);
    }

    public override void Move(float dx, float dy) { CenterX += dx; CenterY += dy; }
}
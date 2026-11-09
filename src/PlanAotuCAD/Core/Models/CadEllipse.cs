using Microsoft.Maui.Graphics;

namespace PlanAotuCAD.Core.Models;

public sealed class CadEllipse : CadEntity
{
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float RadiusX { get; set; }
    public float RadiusY { get; set; }

    public CadEllipse() { }
    public CadEllipse(float x1, float y1, float x2, float y2)
    {
        CenterX = (x1 + x2) / 2f;
        CenterY = (y1 + y2) / 2f;
        RadiusX = MathF.Abs(x2 - x1) / 2f;
        RadiusY = MathF.Abs(y2 - y1) / 2f;
    }

    public override bool HitTest(PointF p, float tolerance)
    {
        if (RadiusX <= 0 || RadiusY <= 0) return false;
        var nx = (p.X - CenterX) / RadiusX;
        var ny = (p.Y - CenterY) / RadiusY;
        var normalized = MathF.Sqrt(nx * nx + ny * ny);
        var scale = MathF.Min(RadiusX, RadiusY);
        return MathF.Abs(normalized - 1f) * scale <= tolerance ||
               (IsFilled && normalized <= 1f);
    }

    public override void Move(float dx, float dy) { CenterX += dx; CenterY += dy; }
}
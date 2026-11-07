using Microsoft.Maui.Graphics;

namespace PlanAotuCAD.Core.Geometry;

public sealed class CadCoordinateTransformer
{
    public float Zoom { get; set; } = 1f;
    public float PanX { get; set; }
    public float PanY { get; set; }

    public PointF ScreenToWorld(PointF point, float width, float height)
    {
        return new PointF(
            (point.X - width / 2f - PanX) / Zoom,
            (height / 2f + PanY - point.Y) / Zoom);
    }

    public PointF WorldToScreen(PointF point, float width, float height)
    {
        return new PointF(
            width / 2f + PanX + point.X * Zoom,
            height / 2f + PanY - point.Y * Zoom);
    }

    public void ZoomBy(float factor)
    {
        if (!float.IsFinite(factor) || factor <= 0)
            return;

        Zoom = Math.Clamp(Zoom * factor, 0.1f, 20f);
    }

    public void ZoomAt(PointF screenPoint, float width, float height, float factor)
    {
        if (!float.IsFinite(factor) || factor <= 0)
            return;

        var worldAnchor = ScreenToWorld(screenPoint, width, height);
        Zoom = Math.Clamp(Zoom * factor, 0.1f, 20f);

        PanX = screenPoint.X - width / 2f - worldAnchor.X * Zoom;
        PanY = screenPoint.Y - height / 2f + worldAnchor.Y * Zoom;
    }
}

using Microsoft.Maui.Graphics;

namespace PlanAotuCAD.Core.Geometry;

public sealed class CadCoordinateTransformer
{
    // نطاق التكبير واسع لأن المخططات المعمارية بوحدة المليمتر قد تتجاوز 20000 وحدة.
    public const float MinZoom = 0.01f;
    public const float MaxZoom = 100f;

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

    public void Reset()
    {
        Zoom = 1f;
        PanX = 0f;
        PanY = 0f;
    }

    public void ZoomBy(float factor)
    {
        if (!float.IsFinite(factor) || factor <= 0)
            return;

        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
    }

    public void ZoomAt(PointF screenPoint, float width, float height, float factor)
    {
        if (!float.IsFinite(factor) || factor <= 0)
            return;

        var worldAnchor = ScreenToWorld(screenPoint, width, height);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);

        PanX = screenPoint.X - width / 2f - worldAnchor.X * Zoom;
        PanY = screenPoint.Y - height / 2f + worldAnchor.Y * Zoom;
    }

    /// <summary>يضبط الكاميرا بحيث يظهر مستطيل العالم كاملاً داخل الشاشة مع هامش بالبكسل.</summary>
    public void ZoomToRect(RectF world, float width, float height, float margin)
    {
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 1f || height <= 1f)
            return;

        var availableWidth = Math.Max(width - margin * 2f, 1f);
        var availableHeight = Math.Max(height - margin * 2f, 1f);
        var isPoint = world.Width <= 0.0001f && world.Height <= 0.0001f;

        float zoom;
        if (isPoint)
        {
            zoom = 1f;
        }
        else
        {
            var zoomX = world.Width > 0.0001f ? availableWidth / world.Width : MaxZoom;
            var zoomY = world.Height > 0.0001f ? availableHeight / world.Height : MaxZoom;
            zoom = Math.Clamp(Math.Min(zoomX, zoomY), MinZoom, MaxZoom);
        }

        var centerX = world.X + world.Width / 2f;
        var centerY = world.Y + world.Height / 2f;

        Zoom = zoom;
        PanX = -centerX * zoom;
        PanY = centerY * zoom;
    }
}

using Microsoft.Maui.Graphics;
using PlanAotuCAD.Core.Models;

namespace PlanAotuCAD.Core.Geometry;

/// <summary>حساب حدود العناصر الظاهرة (لأمر ملاءمة العرض).</summary>
public static class CadBounds
{
    public static RectF? Of(CadDocument document)
    {
        var any = false;
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;

        foreach (var entity in document.Entities)
        {
            var layer = document.GetLayer(entity.LayerId);
            if (!layer.IsVisible || layer.IsFrozen)
                continue;

            foreach (var point in ExtremePoints(entity))
            {
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                    continue;

                any = true;
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        return any ? new RectF(minX, minY, maxX - minX, maxY - minY) : null;
    }

    private static IEnumerable<PointF> ExtremePoints(CadEntity entity)
    {
        switch (entity)
        {
            case CadLine line:
                yield return new PointF(line.StartX, line.StartY);
                yield return new PointF(line.EndX, line.EndY);
                break;
            case CadRectangle rectangle:
                yield return new PointF(rectangle.Left, rectangle.Bottom);
                yield return new PointF(rectangle.Right, rectangle.Top);
                break;
            case CadCircle circle:
                yield return new PointF(circle.CenterX - circle.Radius, circle.CenterY - circle.Radius);
                yield return new PointF(circle.CenterX + circle.Radius, circle.CenterY + circle.Radius);
                break;
            case CadEllipse ellipse:
                yield return new PointF(ellipse.CenterX - ellipse.RadiusX, ellipse.CenterY - ellipse.RadiusY);
                yield return new PointF(ellipse.CenterX + ellipse.RadiusX, ellipse.CenterY + ellipse.RadiusY);
                break;
            case CadTriangle triangle:
                yield return triangle.A;
                yield return triangle.B;
                yield return triangle.C;
                break;
            case CadArc arc:
                // حدود الدائرة الكاملة: تقدير متحفظ يضمن ظهور القوس كاملاً.
                yield return new PointF(arc.CenterX - arc.Radius, arc.CenterY - arc.Radius);
                yield return new PointF(arc.CenterX + arc.Radius, arc.CenterY + arc.Radius);
                break;
            case CadPolyline polyline:
                foreach (var vertex in polyline.Points)
                    yield return new PointF(vertex.X, vertex.Y);
                break;
        }
    }
}

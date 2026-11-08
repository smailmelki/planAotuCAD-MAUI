using Microsoft.Maui.Graphics;
using PlanAotuCAD.Core.Geometry;
using PlanAotuCAD.Core.Models;

namespace PlanAotuCAD.Core.Drawing;

public sealed class CadCanvasDrawable : IDrawable
{
    public CadDocument Document { get; set; }
    public CadCoordinateTransformer Camera { get; }
    public CadEntity? PreviewEntity { get; set; }
    public float GridSpacing { get; set; } = 25f;

    public CadCanvasDrawable(CadDocument document, CadCoordinateTransformer camera)
    {
        Document = document;
        Camera = camera;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        canvas.FillColor = Color.FromArgb("#171B24");
        canvas.FillRectangle(dirtyRect);
        canvas.Translate(dirtyRect.Width / 2f + Camera.PanX, dirtyRect.Height / 2f + Camera.PanY);
        canvas.Scale(Camera.Zoom, -Camera.Zoom);

        DrawGrid(canvas, dirtyRect.Width, dirtyRect.Height);
        DrawAxes(canvas, dirtyRect.Width, dirtyRect.Height);

        foreach (var entity in Document.Entities)
        {
            var layer = Document.GetLayer(entity.LayerId);
            if (layer.IsVisible && !layer.IsFrozen)
                DrawEntity(canvas, entity, layer);
        }

        if (PreviewEntity is not null)
        {
            canvas.SaveState();
            canvas.Alpha = 0.8f;
            DrawEntity(canvas, PreviewEntity, null, Color.FromArgb("#FFD166"));
            canvas.RestoreState();
        }

        canvas.RestoreState();
    }

    private void DrawGrid(ICanvas canvas, float width, float height)
    {
        if (!float.IsFinite(Camera.Zoom) || Camera.Zoom <= 0 ||
            !float.IsFinite(GridSpacing) || GridSpacing <= 0)
            return;

        var left = (-width / 2f - Camera.PanX) / Camera.Zoom;
        var right = (width / 2f - Camera.PanX) / Camera.Zoom;
        var bottom = (-height / 2f - Camera.PanY) / Camera.Zoom;
        var top = (height / 2f - Camera.PanY) / Camera.Zoom;
        canvas.StrokeColor = Color.FromArgb("#29313D");
        canvas.StrokeSize = 1f / Camera.Zoom;

        var startX = MathF.Floor(left / GridSpacing) * GridSpacing;
        var startY = MathF.Floor(bottom / GridSpacing) * GridSpacing;
        for (var x = startX; x <= right && x - startX <= GridSpacing * 500; x += GridSpacing)
            canvas.DrawLine(x, bottom, x, top);
        for (var y = startY; y <= top && y - startY <= GridSpacing * 500; y += GridSpacing)
            canvas.DrawLine(left, y, right, y);
    }

    private void DrawAxes(ICanvas canvas, float width, float height)
    {
        var left = (-width / 2f - Camera.PanX) / Camera.Zoom;
        var right = (width / 2f - Camera.PanX) / Camera.Zoom;
        var bottom = (-height / 2f - Camera.PanY) / Camera.Zoom;
        var top = (height / 2f - Camera.PanY) / Camera.Zoom;
        canvas.StrokeSize = 1.5f / Camera.Zoom;
        canvas.StrokeColor = Color.FromArgb("#D65C5C");
        canvas.DrawLine(left, 0, right, 0);
        canvas.StrokeColor = Color.FromArgb("#55A6D9");
        canvas.DrawLine(0, bottom, 0, top);
    }

    private void DrawEntity(ICanvas canvas, CadEntity entity, CadLayer? layer, Color? overrideColor = null)
    {
        canvas.StrokeSize = Math.Max(0.25f, entity.StrokeWidth) / Camera.Zoom;
        var colorHex = entity.ColorHex ?? layer?.ColorHex;
        canvas.StrokeColor = overrideColor ??
            (entity.IsSelected ? Color.FromArgb("#FBBF24") :
             !string.IsNullOrWhiteSpace(colorHex) ? Color.FromArgb(colorHex) :
             entity switch
             {
                 CadLine => Color.FromArgb("#F4F6FA"),
                 CadRectangle => Color.FromArgb("#4ADE80"),
                 CadCircle => Color.FromArgb("#60A5FA"),
                CadEllipse => Color.FromArgb("#A78BFA"),
                CadTriangle => Color.FromArgb("#F59E0B"),
                CadArc => Color.FromArgb("#FB7185"),
                 _ => Colors.White
             });

        switch (entity)
        {
            case CadLine line:
                canvas.DrawLine(line.StartX, line.StartY, line.EndX, line.EndY);
                break;
            case CadRectangle rectangle:
                if (entity.IsFilled)
                    canvas.FillRectangle(rectangle.Left, rectangle.Bottom, rectangle.Width, rectangle.Height);
                else
                    canvas.DrawRectangle(rectangle.Left, rectangle.Bottom, rectangle.Width, rectangle.Height);
                break;
            case CadCircle circle:
                if (entity.IsFilled)
                    canvas.FillCircle(circle.CenterX, circle.CenterY, circle.Radius);
                else
                    canvas.DrawCircle(circle.CenterX, circle.CenterY, circle.Radius);
                break;
            case CadEllipse ellipse:
                var ellipseRect = new RectF(ellipse.CenterX - ellipse.RadiusX, ellipse.CenterY - ellipse.RadiusY,
                    ellipse.RadiusX * 2f, ellipse.RadiusY * 2f);
                if (entity.IsFilled) canvas.FillEllipse(ellipseRect);
                else canvas.DrawEllipse(ellipseRect);
                break;
            case CadTriangle triangle:
                var a = triangle.A; var b = triangle.B; var c = triangle.C;
                if (entity.IsFilled)
                {
                    var trianglePath = new PathF();
                    trianglePath.MoveTo(a.X, a.Y);
                    trianglePath.LineTo(b.X, b.Y);
                    trianglePath.LineTo(c.X, c.Y);
                    trianglePath.Close();
                    canvas.FillPath(trianglePath);
                }
                else
                {
                    canvas.DrawLine(a, b);
                    canvas.DrawLine(b, c);
                    canvas.DrawLine(c, a);
                }
                break;
            case CadArc arc:
                DrawArcSegments(canvas, arc);
                break;
        }
    }

    private static void DrawArcSegments(ICanvas canvas, CadArc arc)
    {
        const int segments = 48;
        var start = arc.StartAngle * MathF.PI / 180f;
        var sweep = arc.SweepAngle * MathF.PI / 180f;
        var previous = new PointF(arc.CenterX + arc.Radius * MathF.Cos(start),
                                  arc.CenterY + arc.Radius * MathF.Sin(start));
        for (var i = 1; i <= segments; i++)
        {
            var angle = start + sweep * i / segments;
            var next = new PointF(arc.CenterX + arc.Radius * MathF.Cos(angle),
                                  arc.CenterY + arc.Radius * MathF.Sin(angle));
            canvas.DrawLine(previous, next);
            previous = next;
        }
    }
}
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

        canvas.Translate(
            dirtyRect.Width / 2f + Camera.PanX,
            dirtyRect.Height / 2f + Camera.PanY);
        canvas.Scale(Camera.Zoom, -Camera.Zoom);

        DrawGrid(canvas, dirtyRect.Width, dirtyRect.Height);
        DrawAxes(canvas, dirtyRect.Width, dirtyRect.Height);

        foreach (var entity in Document.Entities)
            DrawEntity(canvas, entity);

        if (PreviewEntity is not null)
        {
            canvas.SaveState();
            canvas.Alpha = 0.8f;
            DrawEntity(canvas, PreviewEntity, Color.FromArgb("#FFD166"));
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

    private void DrawEntity(ICanvas canvas, CadEntity entity, Color? overrideColor = null)
    {
        canvas.StrokeSize = (entity.IsSelected ? 3f : 2f) / Camera.Zoom;
        canvas.StrokeColor = overrideColor ?? (entity.IsSelected
            ? Color.FromArgb("#FBBF24")
            : entity switch
            {
                CadLine => Color.FromArgb("#F4F6FA"),
                CadRectangle => Color.FromArgb("#4ADE80"),
                CadCircle => Color.FromArgb("#60A5FA"),
                _ => Colors.White
            });

        switch (entity)
        {
            case CadLine line:
                canvas.DrawLine(line.StartX, line.StartY, line.EndX, line.EndY);
                break;

            case CadRectangle rectangle:
                canvas.DrawRectangle(
                    rectangle.Left,
                    rectangle.Bottom,
                    rectangle.Width,
                    rectangle.Height);
                break;

            case CadCircle circle:
                canvas.DrawCircle(circle.CenterX, circle.CenterY, circle.Radius);
                break;
        }
    }
}

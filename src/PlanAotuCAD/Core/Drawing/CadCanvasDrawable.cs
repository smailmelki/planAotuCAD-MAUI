
using Microsoft.Maui.Graphics;
using PlanAotuCAD.Core.Models;

namespace PlanAotuCAD.Core.Drawing;

public class CadCanvasDrawable : IDrawable
{
    public float GridSpacing { get; set; } = 25f;
// إعدادات الكاميرا
    public float Zoom { get; set; } = 1f;

    public float PanX { get; set; } = 0f;

    public float PanY { get; set; } = 0f;
    public List<CadLine> Lines { get; } = new();

    public CadLine? PreviewLine { get; set; }

public List<CadRectangle> Rectangles { get; } = new();

public CadRectangle? PreviewRectangle { get; set; }

public void Draw(ICanvas canvas, RectF dirtyRect)
{
    float width = dirtyRect.Width;
    float height = dirtyRect.Height;

    // رسم الخلفية
    canvas.FillColor = Color.FromArgb("#171B24");
    canvas.FillRectangle(dirtyRect);

    // حفظ حالة الرسم
    canvas.SaveState();

    // تطبيق إحداثيات الكاميرا
    canvas.Translate(
        width / 2 + PanX,
        height / 2 + PanY);

    canvas.Scale(Zoom, -Zoom);

    // رسم جميع العناصر في نظام إحداثيات موحد
    DrawGrid(canvas, width, height);
    DrawAxes(canvas, width, height);
    DrawLines(canvas, width, height);
    DrawRectangles(canvas, width, height);

    // استعادة حالة الرسم
    canvas.RestoreState();
}
private void DrawGrid(ICanvas canvas, float width, float height)
{
    float left = (-width / 2 - PanX) / Zoom;
    float right = (width / 2 - PanX) / Zoom;

    float bottom = (-height / 2 - PanY) / Zoom;
    float top = (height / 2 - PanY) / Zoom;

    canvas.StrokeColor = Color.FromArgb("#29313D");
    canvas.StrokeSize = 1 / Zoom;

    float startX = MathF.Floor(left / GridSpacing) * GridSpacing;
    float startY = MathF.Floor(bottom / GridSpacing) * GridSpacing;

    for (float x = startX; x <= right; x += GridSpacing)
        canvas.DrawLine(x, bottom, x, top);

    for (float y = startY; y <= top; y += GridSpacing)
        canvas.DrawLine(left, y, right, y);
}

private void DrawAxes(ICanvas canvas, float width, float height)
{
    float left = (-width / 2 - PanX) / Zoom;
    float right = (width / 2 - PanX) / Zoom;

    float bottom = (-height / 2 - PanY) / Zoom;
    float top = (height / 2 - PanY) / Zoom;

    canvas.StrokeColor = Color.FromArgb("#D65C5C");
    canvas.StrokeSize = 1.5f / Zoom;
    canvas.DrawLine(left, 0, right, 0);

    canvas.StrokeColor = Color.FromArgb("#55A6D9");
    canvas.StrokeSize = 1.5f / Zoom;
    canvas.DrawLine(0, bottom, 0, top);

    canvas.FillColor = Colors.White;
    canvas.FillCircle(0, 0, 3 / Zoom);
}
    private void DrawLines(ICanvas canvas, float width, float height)
{
    canvas.StrokeColor = Color.FromArgb("#F4F6FA");
    canvas.StrokeSize = 2;

    // رسم الخطوط المحفوظة
    foreach (var line in Lines)
    {
        canvas.DrawLine(
            line.StartX,
            line.StartY,
            line.EndX,
            line.EndY);
    }

    // رسم معاينة الخط أثناء السحب
    if (PreviewLine != null)
    {
        canvas.StrokeColor = Color.FromArgb("#FFD166");
        canvas.StrokeSize = 2;

        canvas.DrawLine(
            PreviewLine.StartX,
            PreviewLine.StartY,
            PreviewLine.EndX,
            PreviewLine.EndY);
    }
}
  
  private void DrawRectangles(ICanvas canvas, float width, float height)
{
    // رسم المستطيلات المحفوظة
    canvas.StrokeColor = Color.FromArgb("#4ADE80");
    canvas.StrokeSize = 2;

    foreach (var rect in Rectangles)
    {
        canvas.DrawRectangle(
            rect.Left,
            rect.Bottom,
            rect.Width,
            rect.Height);
    }

    // رسم معاينة المستطيل أثناء السحب
    if (PreviewRectangle != null)
    {
        var rect = PreviewRectangle;

        canvas.StrokeColor = Color.FromArgb("#FFD166");
        canvas.StrokeSize = 2;

        canvas.DrawRectangle(
            rect.Left,
            rect.Bottom,
            rect.Width,
            rect.Height);
    }
}
}

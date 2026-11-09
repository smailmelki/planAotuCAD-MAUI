namespace PlanAotuCAD.Core.Tools;

public enum CadTool
{
    Select,
    Line,
    Rectangle,
    Circle,
    Ellipse,
    Triangle,
    Arc,
    Polyline,
    Polygon,
    /// <summary>تحريك مساحة الرسم بإصبع واحد أو بالماوس (لا ينشئ عناصر).</summary>
    Pan
}

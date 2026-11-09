using System.Text.Json.Serialization;
using Microsoft.Maui.Graphics;

namespace PlanAotuCAD.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CadLine), "line")]
[JsonDerivedType(typeof(CadRectangle), "rectangle")]
[JsonDerivedType(typeof(CadCircle), "circle")]
[JsonDerivedType(typeof(CadEllipse), "ellipse")]
[JsonDerivedType(typeof(CadTriangle), "triangle")]
[JsonDerivedType(typeof(CadArc), "arc")]
[JsonDerivedType(typeof(CadPolyline), "polyline")]
public abstract class CadEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string LayerId { get; set; } = "0";
    public string? ColorHex { get; set; }
    public float StrokeWidth { get; set; } = 2f;
    public bool IsFilled { get; set; }

    [JsonIgnore]
    public bool IsSelected { get; set; }

    public abstract bool HitTest(PointF point, float tolerance);
    public abstract void Move(float deltaX, float deltaY);
}

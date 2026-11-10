using System.Text.Json.Serialization;
using Microsoft.Maui.Graphics;

namespace PlanAotuCAD.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CadLine), "line")]
[JsonDerivedType(typeof(CadRectangle), "rectangle")]
[JsonDerivedType(typeof(CadCircle), "circle")]
public abstract class CadEntity
{
    [JsonIgnore]
    public bool IsSelected { get; set; }

    public abstract bool HitTest(PointF point, float tolerance);
    public abstract void Move(float deltaX, float deltaY);
}

namespace PlanAotuCAD.Core.Models;

/// <summary>طبقة مستقلة لتنظيم عناصر الرسم والتحكم في ظهورها وقفلها.</summary>
public sealed class CadLayer
{
    public string Id { get; set; } = "0";
    public string Name { get; set; } = "0";
    public string ColorHex { get; set; } = "#F4F6FA";
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public bool IsFrozen { get; set; }
}

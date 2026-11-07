namespace PlanAotuCAD.Core.Models;

public sealed class CadDocument
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Drawing";
    public List<CadEntity> Entities { get; set; } = new();

    public int EntityCount => Entities.Count;
}

namespace PlanAotuCAD.Core.Models;

public sealed class CadDocument
{
    public int Version { get; set; } = 2;
    public string Name { get; set; } = "Drawing";
    public string Units { get; set; } = "Millimeters";
    public string ActiveLayerId { get; set; } = "0";
    public List<CadLayer> Layers { get; set; } = new()
    {
        new CadLayer()
    };
    public List<CadEntity> Entities { get; set; } = new();

    public int EntityCount => Entities.Count;

    public CadLayer GetLayer(string? layerId) =>
        Layers.FirstOrDefault(layer => layer.Id == layerId) ??
        Layers.FirstOrDefault(layer => layer.Id == "0") ??
        new CadLayer();
}

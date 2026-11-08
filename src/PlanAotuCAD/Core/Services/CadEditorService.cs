using System.Text.Json;
using System.Globalization;
using Microsoft.Maui.Graphics;
using PlanAotuCAD.Core.Models;
using PlanAotuCAD.Core.Tools;

namespace PlanAotuCAD.Core.Services;

public sealed class CadEditorService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private PointF _startPoint;
    private PointF _lastPoint;
    private bool _isMovingSelection;
    private string? _gestureBefore;

    public CadDocument Document { get; private set; } = new();
    public CadTool ActiveTool { get; private set; } = CadTool.Select;
    public CadEntity? PreviewEntity { get; private set; }
    public CadEntity? SelectedEntity { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    private string Snapshot() => JsonSerializer.Serialize(Document, JsonOptions);

    private void Record(string before)
    {
        var after = Snapshot();
        if (before == after) return;
        _undo.Push(before);
        _redo.Clear();
    }

    public bool Undo()
    {
        if (!CanUndo) return false;
        _redo.Push(Snapshot());
        Restore(_undo.Pop());
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        _undo.Push(Snapshot());
        Restore(_redo.Pop());
        return true;
    }

    private void Restore(string json)
    {
        var document = JsonSerializer.Deserialize<CadDocument>(json, JsonOptions) ?? new CadDocument();
        ClearSelection();
        Document = document;
        NormalizeDocument();
    }

    public void SetTool(CadTool tool)
    {
        CancelCurrentOperation();
        ActiveTool = tool;
    }

    public void Begin(PointF point, float zoom)
    {
        _startPoint = point;
        _lastPoint = point;
        _isMovingSelection = false;
        _gestureBefore = null;
        PreviewEntity = null;

        if (ActiveTool == CadTool.Select)
        {
            SelectAt(point, 8f / Math.Max(zoom, 0.1f));
            _isMovingSelection = SelectedEntity is not null;
            if (_isMovingSelection) _gestureBefore = Snapshot();
            return;
        }

        PreviewEntity = ActiveTool == CadTool.Polyline
            ? new CadPolyline { Points = { new CadVertex(point.X, point.Y) } }
            : CreateEntity(point, point);
    }

    public void Update(PointF point)
    {
        if (ActiveTool == CadTool.Select)
        {
            if (_isMovingSelection && SelectedEntity is not null)
                SelectedEntity.Move(point.X - _lastPoint.X, point.Y - _lastPoint.Y);
            _lastPoint = point;
            return;
        }

        if (ActiveTool == CadTool.Polyline && PreviewEntity is CadPolyline polyline)
        {
            var last = polyline.Points[^1];
            var dx = point.X - last.X; var dy = point.Y - last.Y;
            if (dx * dx + dy * dy >= 0.25f)
                polyline.Points.Add(new CadVertex(point.X, point.Y));
        }
        else
        {
            PreviewEntity = CreateEntity(_startPoint, point);
        }
    }

    public void End(PointF point)
    {
        Update(point);
        if (PreviewEntity is not null && IsValid(PreviewEntity))
            AddEntity(PreviewEntity);
        else if (_gestureBefore is not null)
            Record(_gestureBefore);

        PreviewEntity = null;
        _isMovingSelection = false;
        _gestureBefore = null;
    }

    public bool AddEntity(CadEntity entity)
    {
        if (!IsValid(entity)) return false;
        var layer = Document.GetLayer(Document.ActiveLayerId);
        if (layer.IsLocked || layer.IsFrozen || !layer.IsVisible) return false;
        var before = Snapshot();
        entity.LayerId = Document.ActiveLayerId;
        Document.Entities.Add(entity);
        Record(before);
        return true;
    }

    public bool SetActiveLayer(string layerId)
    {
        var layer = Document.Layers.FirstOrDefault(item => item.Id == layerId);
        if (layer is null || layer.IsLocked || layer.IsFrozen || !layer.IsVisible) return false;
        var before = Snapshot();
        Document.ActiveLayerId = layerId;
        Record(before);
        return true;
    }

    public bool AddLayer(string name, string colorHex = "#F4F6FA")
    {
        if (string.IsNullOrWhiteSpace(name) ||
            Document.Layers.Any(layer => string.Equals(layer.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return false;
        var before = Snapshot();
        Document.Layers.Add(new CadLayer { Id = Guid.NewGuid().ToString("N"), Name = name.Trim(), ColorHex = colorHex });
        Record(before);
        return true;
    }

    public bool RenameLayer(string layerId, string name)
    {
        var layer = Document.Layers.FirstOrDefault(x => x.Id == layerId);
        if (layer is null || layer.Id == "0" || string.IsNullOrWhiteSpace(name) ||
            Document.Layers.Any(x => x.Id != layerId && string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return false;
        var before = Snapshot();
        layer.Name = name.Trim();
        Record(before);
        return true;
    }

    public bool SetLayerVisibility(string layerId, bool visible)
    {
        var layer = Document.Layers.FirstOrDefault(x => x.Id == layerId);
        if (layer is null || (layerId == Document.ActiveLayerId && !visible)) return false;
        var before = Snapshot();
        layer.IsVisible = visible;
        Record(before);
        return true;
    }

    public bool SetLayerLocked(string layerId, bool locked)
    {
        var layer = Document.Layers.FirstOrDefault(x => x.Id == layerId);
        if (layer is null || (layerId == Document.ActiveLayerId && locked)) return false;
        var before = Snapshot();
        layer.IsLocked = locked;
        Record(before);
        return true;
    }

    public bool SetLayerFrozen(string layerId, bool frozen)
    {
        var layer = Document.Layers.FirstOrDefault(x => x.Id == layerId);
        if (layer is null || (layerId == Document.ActiveLayerId && frozen)) return false;
        var before = Snapshot();
        layer.IsFrozen = frozen;
        Record(before);
        return true;
    }

    public bool SetLayerColor(string layerId, string colorHex)
    {
        var layer = Document.Layers.FirstOrDefault(x => x.Id == layerId);
        if (layer is null || !IsValidColorHex(colorHex))
            return false;
        var before = Snapshot();
        layer.ColorHex = colorHex;
        Record(before);
        return true;
    }

    public bool MergeLayer(string sourceLayerId, string targetLayerId)
    {
        var source = Document.Layers.FirstOrDefault(x => x.Id == sourceLayerId);
        var target = Document.Layers.FirstOrDefault(x => x.Id == targetLayerId);
        if (source is null || target is null || source.Id == target.Id || source.Id == "0" ||
            target.IsLocked || target.IsFrozen || !target.IsVisible)
            return false;
        var before = Snapshot();
        foreach (var entity in Document.Entities.Where(x => x.LayerId == sourceLayerId))
            entity.LayerId = targetLayerId;
        Document.Layers.Remove(source);
        if (Document.ActiveLayerId == sourceLayerId) Document.ActiveLayerId = targetLayerId;
        Record(before);
        return true;
    }

    public bool DeleteLayer(string layerId)
    {
        var layer = Document.Layers.FirstOrDefault(x => x.Id == layerId);
        if (layer is null || layer.Id == "0" || Document.Layers.Count <= 1) return false;
        var before = Snapshot();
        var fallback = Document.Layers.First(x => x.Id != layerId);
        foreach (var entity in Document.Entities.Where(x => x.LayerId == layerId))
            entity.LayerId = fallback.Id;
        Document.Layers.Remove(layer);
        if (Document.ActiveLayerId == layerId) Document.ActiveLayerId = fallback.Id;
        Record(before);
        return true;
    }

    public bool MoveSelectedToLayer(string layerId)
    {
        var target = Document.Layers.FirstOrDefault(x => x.Id == layerId);
        if (SelectedEntity is null || target is null || target.IsLocked || target.IsFrozen || !target.IsVisible)
            return false;
        var before = Snapshot();
        SelectedEntity.LayerId = layerId;
        Record(before);
        return true;
    }

    public bool UpdateSelectedProperties(string? name, string? colorHex, float strokeWidth, bool isFilled, string? layerId = null)
    {
        var targetLayer = layerId is null ? null : Document.Layers.FirstOrDefault(x => x.Id == layerId);
        if (SelectedEntity is null || IsLayerLocked(SelectedEntity.LayerId) ||
            !float.IsFinite(strokeWidth) || strokeWidth <= 0 ||
            (!string.IsNullOrWhiteSpace(colorHex) && !IsValidColorHex(colorHex)) ||
            (layerId is not null && (targetLayer is null || targetLayer.IsLocked || targetLayer.IsFrozen || !targetLayer.IsVisible)))
            return false;
        var before = Snapshot();
        SelectedEntity.Name = name?.Trim() ?? string.Empty;
        SelectedEntity.ColorHex = string.IsNullOrWhiteSpace(colorHex) ? null : colorHex;
        SelectedEntity.StrokeWidth = strokeWidth;
        SelectedEntity.IsFilled = isFilled;
        if (targetLayer is not null) SelectedEntity.LayerId = targetLayer.Id;
        Record(before);
        return true;
    }

    public void DeleteSelection()
    {
        if (SelectedEntity is null || IsLayerLocked(SelectedEntity.LayerId)) return;
        var before = Snapshot();
        Document.Entities.Remove(SelectedEntity);
        SelectedEntity.IsSelected = false;
        SelectedEntity = null;
        Record(before);
    }

    public void ReplaceDocument(CadDocument document)
    {
        ClearSelection();
        Document = document ?? new CadDocument();
        NormalizeDocument();
        _undo.Clear();
        _redo.Clear();
    }

    private void NormalizeDocument()
    {
        if (Document.Layers is null || Document.Layers.Count == 0)
            Document.Layers = new List<CadLayer> { new() };
        if (Document.Entities is null) Document.Entities = new List<CadEntity>();
        if (!Document.Layers.Any(layer => layer.Id == Document.ActiveLayerId))
            Document.ActiveLayerId = Document.Layers[0].Id;
        foreach (var entity in Document.Entities)
            if (string.IsNullOrWhiteSpace(entity.LayerId) || !Document.Layers.Any(layer => layer.Id == entity.LayerId))
                entity.LayerId = Document.Layers[0].Id;
    }

    public void CancelCurrentOperation()
    {
        PreviewEntity = null;
        _isMovingSelection = false;
        _gestureBefore = null;
    }

    private void SelectAt(PointF point, float tolerance)
    {
        ClearSelection();
        SelectedEntity = Document.Entities.AsEnumerable().Reverse().FirstOrDefault(entity =>
            !IsLayerHidden(entity.LayerId) && !IsLayerLocked(entity.LayerId) && entity.HitTest(point, tolerance));
        if (SelectedEntity is not null) SelectedEntity.IsSelected = true;
    }

    private bool IsLayerHidden(string layerId)
    {
        var layer = Document.GetLayer(layerId);
        return !layer.IsVisible || layer.IsFrozen;
    }

    private bool IsLayerLocked(string layerId) => Document.GetLayer(layerId).IsLocked;

    private void ClearSelection()
    {
        if (SelectedEntity is not null) SelectedEntity.IsSelected = false;
        SelectedEntity = null;
    }

    private CadEntity CreateEntity(PointF start, PointF end) => ActiveTool switch
    {
        CadTool.Line => new CadLine(start.X, start.Y, end.X, end.Y),
        CadTool.Rectangle => new CadRectangle(start.X, start.Y, end.X, end.Y),
        CadTool.Circle => new CadCircle(start.X, start.Y, Distance(start, end)),
        CadTool.Ellipse => new CadEllipse(start.X, start.Y, end.X, end.Y),
        CadTool.Triangle => new CadTriangle(start.X, start.Y, end.X, end.Y),
        CadTool.Arc => new CadArc(start.X, start.Y, end.X, end.Y),
        CadTool.Polygon => CreateRegularPolygon(new PointF((start.X + end.X) / 2f, (start.Y + end.Y) / 2f), Distance(start, end) / 2f, 6),
        _ => null!
    };

    public static CadPolyline CreateRegularPolygon(PointF center, float radius, int sides)
    {
        sides = Math.Clamp(sides, 3, 32);
        var polygon = new CadPolyline { Closed = true };
        for (var i = 0; i < sides; i++)
        {
            var angle = -MathF.PI / 2f + 2f * MathF.PI * i / sides;
            polygon.Points.Add(new CadVertex(
                center.X + radius * MathF.Cos(angle),
                center.Y + radius * MathF.Sin(angle)));
        }
        return polygon;
    }

    private static bool IsValid(CadEntity entity) => entity switch
    {
        CadLine line => float.IsFinite(line.StartX) && float.IsFinite(line.StartY) &&
            float.IsFinite(line.EndX) && float.IsFinite(line.EndY) &&
            Distance(new PointF(line.StartX, line.StartY), new PointF(line.EndX, line.EndY)) > 0.001f,
        CadRectangle rectangle => float.IsFinite(rectangle.StartX) && float.IsFinite(rectangle.StartY) &&
            float.IsFinite(rectangle.EndX) && float.IsFinite(rectangle.EndY) && rectangle.Width > 0.001f && rectangle.Height > 0.001f,
        CadCircle circle => float.IsFinite(circle.CenterX) && float.IsFinite(circle.CenterY) &&
            float.IsFinite(circle.Radius) && circle.Radius > 0.001f,
        CadEllipse ellipse => float.IsFinite(ellipse.CenterX) && float.IsFinite(ellipse.CenterY) &&
            float.IsFinite(ellipse.RadiusX) && float.IsFinite(ellipse.RadiusY) &&
            ellipse.RadiusX > 0.001f && ellipse.RadiusY > 0.001f,
        CadTriangle triangle => float.IsFinite(triangle.X1) && float.IsFinite(triangle.Y1) &&
            float.IsFinite(triangle.X2) && float.IsFinite(triangle.Y2) &&
            MathF.Abs(triangle.X2-triangle.X1) > 0.001f && MathF.Abs(triangle.Y2-triangle.Y1) > 0.001f,
        CadArc arc => float.IsFinite(arc.CenterX) && float.IsFinite(arc.CenterY) &&
            float.IsFinite(arc.Radius) && float.IsFinite(arc.StartAngle) &&
            float.IsFinite(arc.SweepAngle) && arc.Radius > 0.001f &&
            arc.SweepAngle > 0 && arc.SweepAngle <= 360,
        CadPolyline polyline => polyline.Points is not null &&
            polyline.Points.Count >= (polyline.Closed ? 3 : 2) &&
            polyline.Points.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y)),
        _ => false
    };

    private static bool IsValidColorHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] != '#' ||
            (value.Length != 7 && value.Length != 9))
            return false;
        return uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out _);
    }

    private static float Distance(PointF a, PointF b) =>
        MathF.Sqrt(MathF.Pow(b.X-a.X, 2) + MathF.Pow(b.Y-a.Y, 2));
}

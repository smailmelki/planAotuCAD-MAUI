using Microsoft.Maui.Graphics;
using PlanAotuCAD.Core.Models;
using PlanAotuCAD.Core.Tools;

namespace PlanAotuCAD.Core.Services;

public sealed class CadEditorService
{
    private PointF _startPoint;
    private PointF _lastPoint;
    private bool _isMovingSelection;

    public CadDocument Document { get; private set; } = new();
    public CadTool ActiveTool { get; private set; } = CadTool.Select;
    public CadEntity? PreviewEntity { get; private set; }
    public CadEntity? SelectedEntity { get; private set; }

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
        PreviewEntity = null;

        if (ActiveTool == CadTool.Select)
        {
            SelectAt(point, 8f / Math.Max(zoom, 0.1f));
            _isMovingSelection = SelectedEntity is not null;
            return;
        }

        PreviewEntity = CreateEntity(point, point);
    }

    public void Update(PointF point)
    {
        if (ActiveTool == CadTool.Select)
        {
            if (_isMovingSelection && SelectedEntity is not null)
            {
                SelectedEntity.Move(point.X - _lastPoint.X, point.Y - _lastPoint.Y);
            }

            _lastPoint = point;
            return;
        }

        PreviewEntity = CreateEntity(_startPoint, point);
    }

    public void End(PointF point)
    {
        Update(point);

        if (PreviewEntity is not null && IsValid(PreviewEntity))
            Document.Entities.Add(PreviewEntity);

        PreviewEntity = null;
        _isMovingSelection = false;
    }

    public bool AddEntity(CadEntity entity)
    {
        if (!IsValid(entity))
            return false;

        Document.Entities.Add(entity);
        return true;
    }

    public void DeleteSelection()
    {
        if (SelectedEntity is null)
            return;

        Document.Entities.Remove(SelectedEntity);
        SelectedEntity.IsSelected = false;
        SelectedEntity = null;
    }

    public void ReplaceDocument(CadDocument document)
    {
        ClearSelection();
        Document = document ?? new CadDocument();
    }

    public void CancelCurrentOperation()
    {
        PreviewEntity = null;
        _isMovingSelection = false;
    }

    private void SelectAt(PointF point, float tolerance)
    {
        ClearSelection();

        SelectedEntity = Document.Entities
            .AsEnumerable()
            .Reverse()
            .FirstOrDefault(entity => entity.HitTest(point, tolerance));

        if (SelectedEntity is not null)
            SelectedEntity.IsSelected = true;
    }

    private void ClearSelection()
    {
        if (SelectedEntity is not null)
            SelectedEntity.IsSelected = false;

        SelectedEntity = null;
    }

    private CadEntity CreateEntity(PointF start, PointF end)
    {
        return ActiveTool switch
        {
            CadTool.Line => new CadLine(start.X, start.Y, end.X, end.Y),
            CadTool.Rectangle => new CadRectangle(start.X, start.Y, end.X, end.Y),
            CadTool.Circle => new CadCircle(
                start.X,
                start.Y,
                Distance(start, end)),
            _ => null!
        };
    }

    private static bool IsValid(CadEntity entity) => entity switch
    {
        CadLine line =>
            float.IsFinite(line.StartX) && float.IsFinite(line.StartY) &&
            float.IsFinite(line.EndX) && float.IsFinite(line.EndY) &&
            Distance(
                new PointF(line.StartX, line.StartY),
                new PointF(line.EndX, line.EndY)) > 0.001f,

        CadRectangle rectangle =>
            float.IsFinite(rectangle.StartX) && float.IsFinite(rectangle.StartY) &&
            float.IsFinite(rectangle.EndX) && float.IsFinite(rectangle.EndY) &&
            rectangle.Width > 0.001f && rectangle.Height > 0.001f,

        CadCircle circle =>
            float.IsFinite(circle.CenterX) && float.IsFinite(circle.CenterY) &&
            float.IsFinite(circle.Radius) && circle.Radius > 0.001f,

        _ => false
    };

    private static float Distance(PointF a, PointF b)
    {
        return MathF.Sqrt(
            MathF.Pow(b.X - a.X, 2) +
            MathF.Pow(b.Y - a.Y, 2));
    }
}

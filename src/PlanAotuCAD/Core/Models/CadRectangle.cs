namespace PlanAotuCAD.Core.Models;

public class CadRectangle
{
    public float StartX { get; set; }
    public float StartY { get; set; }

    public float EndX { get; set; }
    public float EndY { get; set; }

    public CadRectangle(float startX, float startY, float endX, float endY)
    {
        StartX = startX;
        StartY = startY;
        EndX = endX;
        EndY = endY;
    }

    public float Width => Math.Abs(EndX - StartX);

    public float Height => Math.Abs(EndY - StartY);

    public float Left => Math.Min(StartX, EndX);

    public float Right => Math.Max(StartX, EndX);

    public float Bottom => Math.Min(StartY, EndY);

    public float Top => Math.Max(StartY, EndY);
}
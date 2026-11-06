
namespace PlanAotuCAD.Core.Models;

public class CadLine
{
    public float StartX { get; set; }
    public float StartY { get; set; }

    public float EndX { get; set; }
    public float EndY { get; set; }

    public CadLine(float startX, float startY, float endX, float endY)
    {
        StartX = startX;
        StartY = startY;
        EndX = endX;
        EndY = endY;
    }
}

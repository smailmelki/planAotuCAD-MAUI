using System.Globalization;
using System.Text;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using PlanAotuCAD.Core.Models;

namespace PlanAotuCAD.Core.Services;

public sealed class CadDxfService
{
    public async Task ExportAsync(CadDocument document)
    {
        var path = Path.Combine(
            FileSystem.CacheDirectory,
            $"{MakeSafeFileName(document.Name)}.dxf");

        await File.WriteAllTextAsync(path, BuildDxf(document), Encoding.ASCII);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Export CAD drawing",
            File = new ShareFile(path)
        });
    }

    public async Task<CadDocument?> ImportAsync()
    {
        var file = await FilePicker.Default.PickAsync();
        if (file is null)
            return null;

        if (!string.Equals(Path.GetExtension(file.FileName), ".dxf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("يرجى اختيار ملف DXF صالح.");

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();

        return ParseDxf(text, Path.GetFileNameWithoutExtension(file.FileName));
    }

    public string BuildDxf(CadDocument document)
    {
        var output = new StringBuilder();
        AddPair(output, 0, "SECTION");
        AddPair(output, 2, "ENTITIES");

        foreach (var entity in document.Entities)
        {
            var layerName = document.GetLayer(entity.LayerId).Name;
            switch (entity)
            {
                case CadLine line:
                    WriteLine(output, line.StartX, line.StartY, line.EndX, line.EndY, layerName);
                    break;
                case CadRectangle rectangle:
                    WritePolyline(output, new[]
                    {
                        (rectangle.Left, rectangle.Bottom), (rectangle.Right, rectangle.Bottom),
                        (rectangle.Right, rectangle.Top), (rectangle.Left, rectangle.Top)
                    }, true, layerName);
                    break;
                case CadCircle circle:
                    AddPair(output, 0, "CIRCLE");
                    AddPair(output, 8, layerName);
                    AddPair(output, 10, Format(circle.CenterX));
                    AddPair(output, 20, Format(circle.CenterY));
                    AddPair(output, 30, "0");
                    AddPair(output, 40, Format(circle.Radius));
                    break;
                case CadEllipse ellipse:
                    WriteEllipse(output, ellipse, layerName);
                    break;
                case CadArc arc:
                    WriteArc(output, arc, layerName);
                    break;
                case CadTriangle triangle:
                    WritePolyline(output, new[]
                    {
                        (triangle.A.X, triangle.A.Y), (triangle.B.X, triangle.B.Y),
                        (triangle.C.X, triangle.C.Y)
                    }, true, layerName);
                    break;
                case CadPolyline polyline:
                    WritePolyline(output, polyline.Points.Select(point => (point.X, point.Y)),
                        polyline.Closed, layerName);
                    break;
            }
        }

        AddPair(output, 0, "ENDSEC");
        AddPair(output, 0, "EOF");
        return output.ToString();
    }

    public CadDocument ParseDxf(string content, string documentName = "Imported drawing")
    {
        var lines = content.Replace("\r", string.Empty).Split('\n');
        var document = new CadDocument { Name = documentName };
        var inEntities = false;
        var index = 0;

        while (index + 1 < lines.Length)
        {
            if (!int.TryParse(lines[index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                index++;
                continue;
            }

            var value = lines[index + 1].Trim();
            index += 2;

            if (code == 0 && value == "SECTION")
            {
                if (index + 1 < lines.Length &&
                    int.TryParse(lines[index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sectionCode) &&
                    sectionCode == 2)
                {
                    inEntities = lines[index + 1].Trim() == "ENTITIES";
                    index += 2;
                }
                continue;
            }

            if (code == 0 && value == "ENDSEC")
            {
                inEntities = false;
                continue;
            }

            if (!inEntities || code != 0)
                continue;

            var entityType = value;
            var data = new List<KeyValuePair<int, string>>();

            while (index + 1 < lines.Length &&
                   int.TryParse(lines[index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dataCode) &&
                   dataCode != 0)
            {
                data.Add(new KeyValuePair<int, string>(dataCode, lines[index + 1].Trim()));
                index += 2;
            }

            ImportEntity(document, entityType, data);
        }

        return document;
    }

    private static void ImportEntity(
        CadDocument document,
        string entityType,
        List<KeyValuePair<int, string>> data)
    {
        switch (entityType)
        {
            case "LINE":
                if (TryGetFloat(data, 10, out var x1) &&
                    TryGetFloat(data, 20, out var y1) &&
                    TryGetFloat(data, 11, out var x2) &&
                    TryGetFloat(data, 21, out var y2))
                    document.Entities.Add(new CadLine(x1, y1, x2, y2));
                break;

            case "CIRCLE":
                if (TryGetFloat(data, 10, out var cx) &&
                    TryGetFloat(data, 20, out var cy) &&
                    TryGetFloat(data, 40, out var radius) &&
                    radius > 0)
                    document.Entities.Add(new CadCircle(cx, cy, radius));
                break;

            case "LWPOLYLINE":
                ImportPolyline(document, data);
                break;
        }
    }

    private static void ImportPolyline(
        CadDocument document,
        List<KeyValuePair<int, string>> data)
    {
        var vertices = new List<(float X, float Y)>();
        float? currentX = null;

        foreach (var pair in data)
        {
            if (pair.Key == 10 &&
                float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.IsFinite(x))
            {
                currentX = x;
            }
            else if (pair.Key == 20 && currentX.HasValue &&
                     float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                     float.IsFinite(y))
            {
                vertices.Add((currentX.Value, y));
                currentX = null;
            }
        }

        var closed = TryGetFloat(data, 70, out var flags) && (((int)flags & 1) == 1);
        for (var i = 1; i < vertices.Count; i++)
            document.Entities.Add(new CadLine(vertices[i - 1].X, vertices[i - 1].Y, vertices[i].X, vertices[i].Y));

        if (closed && vertices.Count > 2)
        {
            var first = vertices[0];
            var last = vertices[^1];
            document.Entities.Add(new CadLine(last.X, last.Y, first.X, first.Y));
        }
    }

    private static bool TryGetFloat(
        List<KeyValuePair<int, string>> data,
        int code,
        out float value)
    {
        var pair = data.FirstOrDefault(item => item.Key == code);
        return float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && float.IsFinite(value);
    }

    private static void WritePolyline(
        StringBuilder output,
        IEnumerable<(float X, float Y)> vertices,
        bool closed,
        string layerName)
    {
        var points = vertices.ToArray();
        if (points.Length < (closed ? 3 : 2)) return;
        AddPair(output, 0, "LWPOLYLINE");
        AddPair(output, 8, layerName);
        AddPair(output, 90, points.Length.ToString(CultureInfo.InvariantCulture));
        AddPair(output, 70, closed ? "1" : "0");
        foreach (var point in points)
        {
            AddPair(output, 10, Format(point.X));
            AddPair(output, 20, Format(point.Y));
        }
    }

    private static void WriteEllipse(StringBuilder output, CadEllipse ellipse, string layerName)
    {
        var majorIsX = ellipse.RadiusX >= ellipse.RadiusY;
        var major = MathF.Max(ellipse.RadiusX, ellipse.RadiusY);
        var minor = MathF.Min(ellipse.RadiusX, ellipse.RadiusY);
        AddPair(output, 0, "ELLIPSE");
        AddPair(output, 8, layerName);
        AddPair(output, 10, Format(ellipse.CenterX));
        AddPair(output, 20, Format(ellipse.CenterY));
        AddPair(output, 30, "0");
        AddPair(output, 11, Format(majorIsX ? major : 0));
        AddPair(output, 21, Format(majorIsX ? 0 : major));
        AddPair(output, 31, "0");
        AddPair(output, 40, Format(minor / major));
        AddPair(output, 41, "0");
        AddPair(output, 42, Format(MathF.PI * 2f));
    }

    private static void WriteArc(StringBuilder output, CadArc arc, string layerName)
    {
        var start = (arc.StartAngle % 360f + 360f) % 360f;
        var end = (start + arc.SweepAngle) % 360f;
        AddPair(output, 0, "ARC");
        AddPair(output, 8, layerName);
        AddPair(output, 10, Format(arc.CenterX));
        AddPair(output, 20, Format(arc.CenterY));
        AddPair(output, 30, "0");
        AddPair(output, 40, Format(arc.Radius));
        AddPair(output, 50, Format(start));
        AddPair(output, 51, Format(end));
    }

    private static void WriteLine(StringBuilder output, float x1, float y1, float x2, float y2, string layerName = "0")
    {
        AddPair(output, 0, "LINE");
        AddPair(output, 8, layerName);
        AddPair(output, 10, Format(x1));
        AddPair(output, 20, Format(y1));
        AddPair(output, 30, "0");
        AddPair(output, 11, Format(x2));
        AddPair(output, 21, Format(y2));
        AddPair(output, 31, "0");
    }

    private static void AddPair(StringBuilder output, int code, string value)
    {
        output.AppendLine(code.ToString(CultureInfo.InvariantCulture));
        output.AppendLine(value);
    }

    private static string Format(float value) =>
        value.ToString("0.########", CultureInfo.InvariantCulture);

    private static string MakeSafeFileName(string name)
    {
        var safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        return string.IsNullOrWhiteSpace(safe) ? "Drawing" : safe;
    }
}

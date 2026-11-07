using Microsoft.Maui.Storage;
using System.Text.Json;
using PlanAotuCAD.Core.Models;

namespace PlanAotuCAD.Core.Services;

public sealed class CadDocumentStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _directoryPath =
        Path.Combine(FileSystem.AppDataDirectory, "Drawings");

    public async Task<string> SaveAsync(CadDocument document)
    {
        Directory.CreateDirectory(_directoryPath);

        var safeName = string.Join("_", document.Name.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "Drawing";

        var path = Path.Combine(_directoryPath, $"{safeName}.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions);
        return path;
    }

    public async Task<CadDocument?> OpenAsync()
    {
        var file = await FilePicker.Default.PickAsync();
        if (file is null)
            return null;

        if (!string.Equals(Path.GetExtension(file.FileName), ".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("يرجى اختيار ملف JSON صالح.");

        await using var stream = await file.OpenReadAsync();
        var document = await JsonSerializer.DeserializeAsync<CadDocument>(stream, JsonOptions);

        if (document is null || document.Entities is null)
            throw new InvalidDataException("تعذر قراءة ملف الرسم أو أن بنيته غير صالحة.");

        return document;
    }
}

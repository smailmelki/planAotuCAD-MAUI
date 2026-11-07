using Microsoft.Maui.Storage;
using System.Text.Json;
using PlanAotuCAD.Core.Models;

namespace PlanAotuCAD.Core.Services;

public sealed record CadSavedDrawing(
    string Name,
    string FileName,
    DateTime LastModifiedUtc);

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
        ArgumentNullException.ThrowIfNull(document);

        Directory.CreateDirectory(_directoryPath);

        var safeName = CreateSafeFileName(document.Name);
        var fileName = $"{safeName}.json";
        var path = Path.Combine(_directoryPath, fileName);
        var temporaryPath = Path.Combine(_directoryPath, $"{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions);
                await stream.FlushAsync();
            }

            // استبدال الملف بعد اكتمال الكتابة لتجنب تلف النسخة السابقة عند انقطاع الحفظ.
            File.Move(temporaryPath, path, overwrite: true);
            return fileName;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public Task<IReadOnlyList<CadSavedDrawing>> GetSavedDrawingsAsync()
    {
        Directory.CreateDirectory(_directoryPath);

        IReadOnlyList<CadSavedDrawing> drawings = Directory
            .EnumerateFiles(_directoryPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new CadSavedDrawing(
                Path.GetFileNameWithoutExtension(file.Name),
                file.Name,
                file.LastWriteTimeUtc))
            .ToList();

        return Task.FromResult(drawings);
    }

    public Task<CadDocument> OpenSavedAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("اسم ملف الرسم غير صالح.");
        }

        var path = Path.Combine(_directoryPath, fileName);
        return ReadDocumentAsync(path);
    }

    public async Task<CadDocument?> OpenExternalAsync()
    {
        var file = await FilePicker.Default.PickAsync();
        if (file is null)
            return null;

        if (!string.Equals(Path.GetExtension(file.FileName), ".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("يرجى اختيار ملف JSON صالح.");

        await using var stream = await file.OpenReadAsync();
        return await DeserializeDocumentAsync(stream);
    }

    private static async Task<CadDocument> ReadDocumentAsync(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("ملف الرسم المحفوظ غير موجود.", path);

        await using var stream = File.OpenRead(path);
        return await DeserializeDocumentAsync(stream);
    }

    private static async Task<CadDocument> DeserializeDocumentAsync(Stream stream)
    {
        var document = await JsonSerializer.DeserializeAsync<CadDocument>(stream, JsonOptions);

        if (document?.Entities is null)
            throw new InvalidDataException("تعذر قراءة ملف الرسم أو أن بنيته غير صالحة.");

        document.Name = string.IsNullOrWhiteSpace(document.Name)
            ? "Drawing"
            : document.Name;

        return document;
    }

    private static string CreateSafeFileName(string? name)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars()
            .Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
            .ToHashSet();

        var safeName = new string((name ?? string.Empty)
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim()
            .TrimEnd('.');

        return string.IsNullOrWhiteSpace(safeName) ? "Drawing" : safeName;
    }
}

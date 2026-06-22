using System.Text.Json;
using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public static class JsonExportService
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static void ExportSnapshot(string filePath, InspectorSnapshot snapshot) => File.WriteAllText(filePath, Serialize(snapshot));
}

using System.Text.Json.Serialization;

namespace CyberPDF.Common.Settings;

/// <summary>
/// Source-generated JSON metadata for <c>annotations.json</c>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(AnnotationSet))]
[JsonSerializable(typeof(DocumentAnnotations))]
[JsonSerializable(typeof(TextHighlight))]
[JsonSerializable(typeof(PersonalBookmark))]
internal sealed partial class AnnotationJsonContext : JsonSerializerContext
{
}

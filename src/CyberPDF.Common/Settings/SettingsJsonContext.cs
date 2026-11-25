using System.Text.Json.Serialization;

namespace CyberPDF.Common.Settings;

/// <summary>
/// Source-generated JSON metadata for the settings file. Source generation keeps
/// the serializer working under trimming and NativeAOT without reflection, and
/// turns a missing property mapping into a build error instead of a silent
/// default at runtime.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}

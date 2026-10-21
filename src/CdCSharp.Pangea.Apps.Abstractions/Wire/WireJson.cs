using System.Text.Json;
using System.Text.Json.Serialization;

namespace CdCSharp.Pangea.Apps.Wire;

/// <summary>How everything on the bus is serialised, by both ends.</summary>
/// <remarks>
/// Web defaults - camel case, case-insensitive reading - with enums as their names. Names rather
/// than numbers because the two ends are built separately: a value added in the middle of an enum
/// shifts every number after it, and the receiver reads the wrong thing without noticing. A name
/// it does not know fails loudly instead.
/// </remarks>
public static class WireJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}

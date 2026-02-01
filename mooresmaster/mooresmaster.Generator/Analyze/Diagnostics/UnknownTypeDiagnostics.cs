using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     スキーマのtypeが不明な値の場合のDiagnostics
/// </summary>
public class UnknownTypeDiagnostics : IDiagnostics
{
    public UnknownTypeDiagnostics(JsonObject json, string? propertyName, string unknownType, Location typeLocation)
    {
        Json = json;
        PropertyName = propertyName;
        UnknownType = unknownType;
        Locations = new[] { typeLocation };
    }

    public JsonObject Json { get; }
    public string? PropertyName { get; }
    public string UnknownType { get; }
    public Location[] Locations { get; }

    public string Message => PropertyName != null
        ? $"Property '{PropertyName}' has unknown type '{UnknownType}'. Valid types are: object, array, string, enum, number, integer, boolean, uuid, vector2, vector3, vector4, vector2Int, vector3Int."
        : $"Schema has unknown type '{UnknownType}'. Valid types are: object, array, string, enum, number, integer, boolean, uuid, vector2, vector3, vector4, vector2Int, vector3Int.";
}

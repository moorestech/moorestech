using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     objectのpropertiesがJsonArrayでない場合のDiagnostics
/// </summary>
public class ObjectPropertiesNotArrayDiagnostics : IDiagnostics
{
    public ObjectPropertiesNotArrayDiagnostics(string? propertyName, IJsonNode actualNode)
    {
        PropertyName = propertyName;
        ActualNode = actualNode;
        Locations = new[] { actualNode.Location };
    }

    public string? PropertyName { get; }
    public IJsonNode ActualNode { get; }
    public Location[] Locations { get; }

    public string Message => PropertyName != null
        ? $"'properties' in object '{PropertyName}' must be an array."
        : "'properties' in object must be an array.";
}

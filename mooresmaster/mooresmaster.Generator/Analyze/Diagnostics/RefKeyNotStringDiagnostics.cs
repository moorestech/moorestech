using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     refの値がJsonStringでない場合のDiagnostics
/// </summary>
public class RefKeyNotStringDiagnostics : IDiagnostics
{
    public RefKeyNotStringDiagnostics(string? propertyName, IJsonNode actualNode)
    {
        PropertyName = propertyName;
        ActualNode = actualNode;
        Locations = new[] { actualNode.Location };
    }

    public string? PropertyName { get; }
    public IJsonNode ActualNode { get; }
    public Location[] Locations { get; }

    public string Message => PropertyName != null
        ? $"'ref' value in property '{PropertyName}' must be a string."
        : "'ref' value must be a string.";
}

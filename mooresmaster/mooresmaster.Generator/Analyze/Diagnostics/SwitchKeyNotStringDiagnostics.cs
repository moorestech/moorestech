using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     switchの値がJsonStringでない場合のDiagnostics
/// </summary>
public class SwitchKeyNotStringDiagnostics : IDiagnostics
{
    public SwitchKeyNotStringDiagnostics(string? propertyName, IJsonNode actualNode)
    {
        PropertyName = propertyName;
        ActualNode = actualNode;
        Locations = new[] { actualNode.Location };
    }

    public string? PropertyName { get; }
    public IJsonNode ActualNode { get; }
    public Location[] Locations { get; }

    public string Message => PropertyName != null
        ? $"'switch' value in property '{PropertyName}' must be a string path."
        : "'switch' value must be a string path.";
}

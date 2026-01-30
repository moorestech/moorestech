using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     cases配列の要素がJsonObjectでない場合のDiagnostics
/// </summary>
public class SwitchCaseNotObjectDiagnostics : IDiagnostics
{
    public SwitchCaseNotObjectDiagnostics(string? propertyName, IJsonNode actualNode, int index)
    {
        PropertyName = propertyName;
        ActualNode = actualNode;
        Index = index;
        Locations = new[] { actualNode.Location };
    }

    public string? PropertyName { get; }
    public IJsonNode ActualNode { get; }
    public int Index { get; }
    public Location[] Locations { get; }

    public string Message => PropertyName != null
        ? $"Case at index {Index} in switch property '{PropertyName}' must be an object."
        : $"Case at index {Index} in switch must be an object.";
}

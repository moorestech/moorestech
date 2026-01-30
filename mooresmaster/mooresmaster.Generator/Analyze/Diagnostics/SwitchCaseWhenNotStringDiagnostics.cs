using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     caseのwhenがJsonStringでない、または存在しない場合のDiagnostics
/// </summary>
public class SwitchCaseWhenNotStringDiagnostics : IDiagnostics
{
    public SwitchCaseWhenNotStringDiagnostics(string? propertyName, JsonObject caseNode, IJsonNode? actualWhenNode, int index)
    {
        PropertyName = propertyName;
        CaseNode = caseNode;
        ActualWhenNode = actualWhenNode;
        Index = index;
        Locations = actualWhenNode != null ? new[] { actualWhenNode.Location } : new[] { caseNode.Location };
    }

    public string? PropertyName { get; }
    public JsonObject CaseNode { get; }
    public IJsonNode? ActualWhenNode { get; }
    public int Index { get; }
    public Location[] Locations { get; }

    public string Message => ActualWhenNode == null
        ? (PropertyName != null
            ? $"Case at index {Index} in switch property '{PropertyName}' is missing required 'when' field."
            : $"Case at index {Index} in switch is missing required 'when' field.")
        : (PropertyName != null
            ? $"'when' value at case index {Index} in switch property '{PropertyName}' must be a string."
            : $"'when' value at case index {Index} in switch must be a string.");
}

using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     enum/optionsの配列要素がJsonStringでない場合のDiagnostics
/// </summary>
public class EnumElementNotStringDiagnostics : IDiagnostics
{
    public EnumElementNotStringDiagnostics(string? propertyName, IJsonNode actualNode, int index, bool isEnumType)
    {
        PropertyName = propertyName;
        ActualNode = actualNode;
        Index = index;
        IsEnumType = isEnumType;
        Locations = new[] { actualNode.Location };
    }

    public string? PropertyName { get; }
    public IJsonNode ActualNode { get; }
    public int Index { get; }
    public bool IsEnumType { get; }
    public Location[] Locations { get; }

    public string Message => PropertyName != null
        ? $"Element at index {Index} in '{(IsEnumType ? "options" : "enum")}' array of property '{PropertyName}' must be a string."
        : $"Element at index {Index} in '{(IsEnumType ? "options" : "enum")}' array must be a string.";
}

using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     defineInterfaceまたはglobalDefineInterfaceの配列要素がオブジェクトでない場合のDiagnostics
/// </summary>
public class DefineInterfaceElementNotObjectDiagnostics : IDiagnostics
{
    public DefineInterfaceElementNotObjectDiagnostics(string fieldName, IJsonNode actualNode, int index, bool isGlobal)
    {
        FieldName = fieldName;
        ActualNode = actualNode;
        Index = index;
        IsGlobal = isGlobal;
        Locations = new[] { actualNode.Location };
    }

    public string FieldName { get; }
    public IJsonNode ActualNode { get; }
    public int Index { get; }
    public bool IsGlobal { get; }
    public Location[] Locations { get; }

    public string Message => $"Element at index {Index} in '{FieldName}' must be an object with interface definition.";
}

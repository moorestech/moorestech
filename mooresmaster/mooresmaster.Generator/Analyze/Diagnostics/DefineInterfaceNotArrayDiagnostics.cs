using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     defineInterfaceまたはglobalDefineInterfaceの値が配列でない場合のDiagnostics
/// </summary>
public class DefineInterfaceNotArrayDiagnostics : IDiagnostics
{
    public DefineInterfaceNotArrayDiagnostics(string fieldName, IJsonNode actualNode, bool isGlobal)
    {
        FieldName = fieldName;
        ActualNode = actualNode;
        IsGlobal = isGlobal;
        Locations = new[] { actualNode.Location };
    }

    public string FieldName { get; }
    public IJsonNode ActualNode { get; }
    public bool IsGlobal { get; }
    public Location[] Locations { get; }

    public string Message => IsGlobal
        ? $"'{FieldName}' must be an array of interface definitions."
        : $"'{FieldName}' must be an array of interface definitions.";
}

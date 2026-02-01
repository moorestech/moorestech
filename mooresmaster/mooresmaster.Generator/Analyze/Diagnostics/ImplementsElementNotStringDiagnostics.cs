using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     defineInterfaceのimplements配列の要素がJsonStringでない場合のDiagnostics
/// </summary>
public class ImplementsElementNotStringDiagnostics : IDiagnostics
{
    public ImplementsElementNotStringDiagnostics(string interfaceName, IJsonNode actualNode, int index, bool isGlobal)
    {
        InterfaceName = interfaceName;
        ActualNode = actualNode;
        Index = index;
        IsGlobal = isGlobal;
        Locations = new[] { actualNode.Location };
    }

    public string InterfaceName { get; }
    public IJsonNode ActualNode { get; }
    public int Index { get; }
    public bool IsGlobal { get; }
    public Location[] Locations { get; }

    public string Message => $"Element at index {Index} in 'implements' array of interface '{InterfaceName}' must be a string.";
}

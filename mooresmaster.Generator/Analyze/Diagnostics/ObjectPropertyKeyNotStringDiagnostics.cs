using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     objectのpropertiesの要素のkeyがJsonStringでない場合のDiagnostics
/// </summary>
public class ObjectPropertyKeyNotStringDiagnostics : IDiagnostics
{
    public ObjectPropertyKeyNotStringDiagnostics(string? parentObjectName, JsonObject propertyNode, IJsonNode? actualKeyNode, int propertyIndex)
    {
        ParentObjectName = parentObjectName;
        PropertyNode = propertyNode;
        ActualKeyNode = actualKeyNode;
        PropertyIndex = propertyIndex;
        Locations = actualKeyNode != null ? new[] { actualKeyNode.Location } : new[] { propertyNode.Location };
    }

    public string? ParentObjectName { get; }
    public JsonObject PropertyNode { get; }
    public IJsonNode? ActualKeyNode { get; }
    public int PropertyIndex { get; }
    public Location[] Locations { get; }

    public string Message
    {
        get
        {
            var context = ParentObjectName != null ? $" in object '{ParentObjectName}'" : "";
            return ActualKeyNode == null
                ? $"Property at index {PropertyIndex}{context} is missing required 'key' field."
                : $"Property 'key' at index {PropertyIndex}{context} must be a string.";
        }
    }
}

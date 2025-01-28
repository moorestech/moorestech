using System.Collections.Generic;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator;

public class DefineInterface(
    string rootSchemaId,
    string interfaceName,
    Dictionary<string, IDefineInterfacePropertySchema> properties,
    string[] implementationInterfaces,
    Dictionary<string, JsonString> implementationNodes,
    bool isGlobal
)
{
    public string[] ImplementationInterfaces = implementationInterfaces;
    public Dictionary<string, JsonString> ImplementationNodes = implementationNodes;
    public string InterfaceName = interfaceName;
    public bool IsGlobal = isGlobal;
    public Dictionary<string, IDefineInterfacePropertySchema> Properties = properties;
    public string RootSchemaId = rootSchemaId;
}

using System.Collections.Generic;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator;

public class DefineInterface(string interfaceName, Dictionary<string, IDefineInterfacePropertySchema> properties, string[] implementationInterfaces, bool isGlobal)
{
    public string[] ImplementationInterfaces = implementationInterfaces;
    public string InterfaceName = interfaceName;
    public bool IsGlobal = isGlobal;
    public Dictionary<string, IDefineInterfacePropertySchema> Properties = properties;
}

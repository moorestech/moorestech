using System.Collections.Generic;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator;

public class DefineInterface(string interfaceName, Dictionary<string, IDefineInterfacePropertySchema> properties, string[] implementationInterfaces)
{
    public string[] ImplementationInterfaces = implementationInterfaces;
    public string InterfaceName = interfaceName;
    public Dictionary<string, IDefineInterfacePropertySchema> Properties = properties;
}

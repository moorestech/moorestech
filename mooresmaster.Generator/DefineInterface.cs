using System.Collections.Generic;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator;

public class DefineInterface(string interfaceName, Dictionary<string, IDefineInterfacePropertySchema> properties)
{
    public string InterfaceName = interfaceName;
    public Dictionary<string, IDefineInterfacePropertySchema> Properties = properties;
}

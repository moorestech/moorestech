using System.Collections.Generic;
using mooresmaster.Generator.Common;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator;

public class DefineInterface(
    string rootSchemaId,
    string interfaceName,
    Dictionary<string, Falliable<IDefineInterfacePropertySchema>> properties,
    string[] implementationInterfaces,
    Dictionary<string, JsonString> implementationNodes,
    Dictionary<string, Location[]> duplicateImplementationLocations,
    bool isGlobal,
    Location location
)
{
    public Dictionary<string, Location[]> DuplicateImplementationLocations = duplicateImplementationLocations;
    public string[] ImplementationInterfaces = implementationInterfaces;
    public Dictionary<string, JsonString> ImplementationNodes = implementationNodes;
    public string InterfaceName = interfaceName;
    public bool IsGlobal = isGlobal;
    public Location Location = location;
    public Dictionary<string, Falliable<IDefineInterfacePropertySchema>> Properties = properties;
    public string RootSchemaId = rootSchemaId;
}
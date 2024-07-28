using System.Collections.Generic;
using mooresmaster.Generator.NameResolve;

namespace mooresmaster.Generator.Definitions;

public record Definition
{
    public readonly List<InterfaceDefinition> InterfaceDefinitions = new();
    public readonly List<TypeDefinition> TypeDefinitions = new();
}

public record InterfaceDefinition(TypeName TypeName)
{
    public TypeName TypeName = TypeName;
}

public record TypeDefinition(TypeName TypeName, TypeName[] InheritList, Dictionary<string, Type> PropertyTable)
{
    public TypeName[] InheritList = InheritList;
    public Dictionary<string, Type> PropertyTable = PropertyTable;
    public TypeName TypeName { get; } = TypeName;
}

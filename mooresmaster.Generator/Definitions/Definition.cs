using System.Collections.Generic;

namespace mooresmaster.Generator.Definitions;

public record Definition
{
    public readonly List<InterfaceDefinition> InterfaceDefinitions = new();
    public readonly List<TypeDefinition> TypeDefinitions = new();
}

public record InterfaceDefinition(string Name)
{
    public string Name = Name;
}

public record TypeDefinition(string Name, string[] InheritList, Dictionary<string, Type> PropertyTable)
{
    public string[] InheritList = InheritList;
    public Dictionary<string, Type> PropertyTable = PropertyTable;
    public string Name { get; } = Name;
}

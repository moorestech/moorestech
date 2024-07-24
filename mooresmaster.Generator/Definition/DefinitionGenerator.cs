using System.Collections.Generic;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Definition;

public record Definition
{
    public readonly List<InterfaceDefinition> InterfaceDefinitions = new();
    public readonly List<TypeDefinition> TypeDefinitions = new();
}

public record InterfaceDefinition(string Name)
{
    public string Name = Name;
}

public record TypeDefinition(string Name, string[] InheritList)
{
    public string[] InheritList = InheritList;
    public string Name { get; } = Name;
}

public static class DefinitionGenerator
{
    public static Definition Generate(Semantics semantics)
    {
        var definitions = new Definition();
        
        foreach (var interfaceSemantics in semantics.InterfaceSemantics.Values) definitions.InterfaceDefinitions.Add(new InterfaceDefinition(interfaceSemantics.Name));
        var inheritTable = new Dictionary<string, List<string>>();
        foreach (var inherit in semantics.InheritList)
        {
            if (!inheritTable.TryGetValue(inherit.typeName, out var interfaceList))
            {
                inheritTable[inherit.typeName] = [];
                interfaceList = inheritTable[inherit.typeName];
            }
            
            interfaceList.Add(inherit.interfaceName);
        }
        
        foreach (var typeSemantics in semantics.TypeSemantics.Values)
        {
            var isInherited = inheritTable.TryGetValue(typeSemantics.Name, out var interfaceList);
            definitions.TypeDefinitions.Add(new TypeDefinition(typeSemantics.Name, isInherited ? interfaceList!.ToArray() : []));
        }
        
        return definitions;
    }
}

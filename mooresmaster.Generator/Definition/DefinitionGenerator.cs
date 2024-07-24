using System;
using System.Collections.Generic;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Definition;

public record Definition
{
    public readonly List<InterfaceDefinition> InterfaceDefinitions = new();
    public readonly List<ITypeDefinition> TypeDefinitions = new();
}

public record InterfaceDefinition(string Name)
{
    public string Name = Name;
}

public interface ITypeDefinition
{
    string Name { get; }
}

public record TypeDefinition(string Name) : ITypeDefinition
{
    public string Name { get; } = Name;
}

public record InheritedTypeDefinition(TypeDefinition TypeDefinition) : ITypeDefinition
{
    public TypeDefinition TypeDefinition = TypeDefinition;
    public string Name => TypeDefinition.Name;
}

public static class DefinitionGenerator
{
    public static Definition Generate(Semantics semantics)
    {
        var definitions = new Definition();
        
        foreach (var interfaceSemantics in semantics.InterfaceSemantics.Values) definitions.InterfaceDefinitions.Add(new InterfaceDefinition(interfaceSemantics.Name));
        foreach (var iTypeSemantics in semantics.TypeSemantics.Values)
            switch (iTypeSemantics)
            {
                case InheritedTypeSemantics inheritedTypeSemantics:
                    definitions.TypeDefinitions.Add(new InheritedTypeDefinition(new TypeDefinition(inheritedTypeSemantics.Name)));
                    break;
                case TypeSemantics typeSemantics:
                    definitions.TypeDefinitions.Add(new TypeDefinition(typeSemantics.Name));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(iTypeSemantics));
            }
        
        return definitions;
    }
}

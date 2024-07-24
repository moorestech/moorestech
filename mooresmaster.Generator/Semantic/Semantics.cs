using System.Collections.Generic;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Semantic;

public class Semantics
{
    public readonly List<(string interfaceName, string typeName)> InheritList = new();
    public readonly Dictionary<string, InterfaceSemantics> InterfaceSemantics = new();
    public readonly Dictionary<ObjectSchema, string> ObjectSchemaToType = new();
    public readonly Dictionary<OneOfSchema, string> OneOfToInterface = new();
    public readonly Dictionary<string, TypeSemantics> TypeSemantics = new();
    
    public Semantics Merge(Semantics other)
    {
        foreach (var interfaceSemantics in other.InterfaceSemantics)
            InterfaceSemantics[interfaceSemantics.Key] = interfaceSemantics.Value;
        
        foreach (var typeSemantics in other.TypeSemantics)
            TypeSemantics[typeSemantics.Key] = typeSemantics.Value;
        
        foreach (var inherit in other.InheritList)
            InheritList.Add(inherit);
        
        foreach (var objectSchema in other.ObjectSchemaToType)
            ObjectSchemaToType[objectSchema.Key] = objectSchema.Value;
        
        foreach (var oneOf in other.OneOfToInterface)
            OneOfToInterface[oneOf.Key] = oneOf.Value;
        
        return this;
    }
    
    public Semantics AddTo(Semantics other)
    {
        return other.Merge(this);
    }
}

public record TypeSemantics(string Name, ISchema Schema)
{
    public ISchema Schema { get; } = Schema;
    public string Name { get; } = Name;
}

public record InterfaceSemantics(string Name, ISchema Schema)
{
    public string Name = Name;
    public ISchema Schema = Schema;
}

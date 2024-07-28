using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.NameResolve;

public record struct TypeName(string Name, string NameSpace);

public class NameTable(Dictionary<Guid, TypeName> names)
{
//    public readonly Dictionary<string, Guid> Ids = names.ToDictionary(x => x.Value, x => x.Key);
    public readonly Dictionary<Guid, TypeName> Names = names;
}

public static class NameResolver
{
    public static NameTable Resolve(Semantics semantics, SchemaTable schemaTable)
    {
        var names = new Dictionary<Guid, string>();
        
        // root以外の全てのtypeの名前を登録
        foreach (var kvp in semantics.TypeSemanticsTable)
        {
            var id = kvp.Key;
            var typeSemantics = kvp.Value!;
            
            if (typeSemantics.Schema.Parent is null) continue;
            var name = schemaTable.Table[typeSemantics.Schema.Parent!.Value] switch
            {
                ObjectSchema => typeSemantics.Schema.PropertyName!,
                ArraySchema arraySchema => $"{arraySchema.PropertyName!}Element",
                OneOfSchema oneOfSchema => typeSemantics.Schema.PropertyName!,
                _ => null
            };
            
            if (name is not null) names[id] = name;
        }
        
        // rootのtypeの名前を登録
        foreach (var kvp in semantics.RootSemanticsTable)
        {
            var typeId = kvp.Value.TypeId;
            var root = kvp.Value!;
            
            names[typeId] = root.Root.SchemaId;
        }
        
        // interfaceの名前を登録
        foreach (var kvp in semantics.InterfaceSemanticsTable)
        {
            var id = kvp.Key;
            var interfaceSemantics = kvp.Value!;
            
            names[id] = $"I{interfaceSemantics.Schema.PropertyName!}";
        }
        
        var nameSpaces = new Dictionary<Guid, string>();
        
        foreach (var typeId in names.Keys)
        {
            // child → parent
            var parentNames = new List<string>();
            var schema = semantics.TypeSemanticsTable.ContainsKey(typeId)
                ? semantics.TypeSemanticsTable[typeId].Schema.Parent
                : semantics.InterfaceSemanticsTable[typeId].Schema.Parent;
            
            while (schema is not null)
                switch (schemaTable.Table[schema.Value])
                {
                    case ArraySchema arraySchema:
                        if (parentNames.Count != 0) parentNames[parentNames.Count - 1] = $"{arraySchema.PropertyName!}Element";
                        
                        parentNames.Add(arraySchema.PropertyName!);
                        schema = arraySchema.Parent;
                        break;
                    case ObjectSchema objectSchema:
                        parentNames.Add(objectSchema.PropertyName!);
                        schema = objectSchema.Parent;
                        break;
                    case OneOfSchema oneOfSchema:
                        parentNames.Add(oneOfSchema.PropertyName!);
                        schema = oneOfSchema.Parent;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(schema));
                }
            
            nameSpaces[typeId] = $"mooresmaster{string.Join("", ((IEnumerable<string>)parentNames).Reverse().Select(n => $".{n}"))}";
        }
        
        return new NameTable(names
            .Select(name =>
                new KeyValuePair<Guid, TypeName>(
                    name.Key,
                    new TypeName(name.Value, nameSpaces[name.Key])
                )
            )
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
            ));
    }
    
    public static string GetName(this TypeName typeName)
    {
        return $"{typeName.NameSpace}.{typeName.Name}";
    }
}

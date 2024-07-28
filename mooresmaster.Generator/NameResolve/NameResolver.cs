using System;
using System.Collections.Generic;
using System.Linq;
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
    public static NameTable Resolve(Semantics semantics)
    {
        var names = new Dictionary<Guid, string>();

        // root以外の全てのtypeの名前を登録
        foreach (var kvp in semantics.TypeSemanticsTable)
        {
            var id = kvp.Key;
            var typeSemantics = kvp.Value!;

            var name = typeSemantics.Schema.PropertyName;
            if (name is not null) names[id] = typeSemantics.Schema.PropertyName!;
        }

        // rootのtypeの名前を登録
        foreach (var kvp in semantics.RootSemanticsTable)
        {
            var typeId = kvp.Value.TypeId;
            var root = kvp.Value!;

            names[typeId] = root.Root.SchemaId;
        }

        var nameSpaces = new Dictionary<Guid, string>();

        foreach (var typeId in names.Keys)
        {
            // child → parent
            var parentNames = new List<string>();

            var id = typeId;
            while (semantics.TypeSemanticsTable[id].ParentType is not null)
            {
                id = semantics.TypeSemanticsTable[id].ParentType!.Value;
                parentNames.Add(names[id]);
            }

            nameSpaces[typeId] = $"mooresmaster{string.Join("", ((IEnumerable<string>)parentNames).Reverse().Select(n => $".{n}"))}";
        }

        // interfaceの名前を登録
        foreach (var kvp in semantics.InterfaceSemanticsTable)
        {
            var id = kvp.Key;
            var interfaceSemantics = kvp.Value!;

            names[id] = $"I{interfaceSemantics.Schema.PropertyName!}";
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

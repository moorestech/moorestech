using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.NameResolve;

public record struct TypeName(string Name, string NameSpace);

public class NameTable(Dictionary<Guid, TypeName> names, Dictionary<Guid, string> propertyNames)
{
//    public readonly Dictionary<string, Guid> Ids = names.ToDictionary(x => x.Value, x => x.Key);
    public readonly Dictionary<Guid, TypeName> Names = names;
    public readonly Dictionary<Guid, string> PropertyNames = propertyNames;
}

public static class NameResolver
{
    public static NameTable Resolve(Semantics semantics, SchemaTable schemaTable)
    {
        var names = new Dictionary<Guid, string>();
        var propertyNames = new Dictionary<Guid, string>();

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
                OneOfSchema oneOfSchema => GetIfThenName(oneOfSchema.IfThenArray.ToDictionary(ifThen => schemaTable.Table[ifThen.Then])[typeSemantics.Schema]),
                _ => null
            };

            if (name is not null) names[id] = name.ToCamelCase();
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

            var interfaceName = interfaceSemantics.Schema.PropertyName?.ToCamelCase();
            names[id] = $"I{interfaceName}";
        }

        // namespaceを登録
        var nameSpaces = new Dictionary<Guid, string>();
        var schemaToRoot = semantics.RootSemanticsTable.ToDictionary(r => semantics.TypeSemanticsTable[r.Value.TypeId].Schema, r => r.Value);

        foreach (var typeId in names.Keys)
        {
            // child → parent
            var parentNames = new List<string>();
            var schema = semantics.TypeSemanticsTable.ContainsKey(typeId)
                ? semantics.TypeSemanticsTable[typeId].Schema.Parent
                : semantics.InterfaceSemanticsTable[typeId].Schema.Parent;

            while (schema is not null)
            {
                if (schemaTable.Table[schema.Value].Parent is null)
                {
                    parentNames.Add(schemaToRoot[schemaTable.Table[schema.Value]].Root.SchemaId);
                    break;
                }

                switch (schemaTable.Table[schema.Value])
                {
                    case ArraySchema arraySchema:
                        if (parentNames.Count != 0) parentNames[parentNames.Count - 1] = $"{arraySchema.PropertyName}Element";

                        parentNames.Add(arraySchema.PropertyName);
                        schema = arraySchema.Parent;
                        break;
                    case ObjectSchema objectSchema:
                        parentNames.Add(objectSchema.PropertyName);
                        schema = objectSchema.Parent;
                        break;
                    case OneOfSchema oneOfSchema:
                        parentNames.Add(oneOfSchema.PropertyName);
                        schema = oneOfSchema.Parent;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(schema));
                }
            }

            //一応残しておく nameSpaces[typeId] = $"mooresmaster{string.Join("", ((IEnumerable<string>)parentNames).Reverse().Select(n => $".{n}Module"))}";
            var nameSpace = "Mooresmaster.Model";
            if (0 < parentNames.Count)
            {
                var lastName = parentNames[parentNames.Count - 1].ToCamelCase();
                nameSpace += $".{lastName}Module";
            }

            nameSpaces[typeId] = nameSpace;
        }

        // propertyの名前を登録
        foreach (var kvp in semantics.PropertySemanticsTable)
        {
            var propertySemantics = kvp.Value;
            var name = propertySemantics.PropertyName.ToCamelCase();
            propertyNames[kvp.Key] = name;
        }

        return new NameTable(
            names
                .Select(name =>
                    new KeyValuePair<Guid, TypeName>(
                        name.Key,
                        new TypeName(name.Value, nameSpaces[name.Key])
                    )
                )
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                ),
            propertyNames
        );
    }

    private static string GetIfThenName(IfThenSchema ifThenSchema)
    {
        var jsonObjectStack = new Stack<JsonObject>();
        jsonObjectStack.Push(ifThenSchema.If);

        while (jsonObjectStack.Count > 0)
        {
            var jsonObject = jsonObjectStack.Pop();

            foreach (var node in jsonObject.Nodes.Values)
                switch (node)
                {
                    case JsonObject o:
                        jsonObjectStack.Push(o);
                        break;
                    case JsonString jsonString:
                        return jsonString.Literal;
                    case JsonArray jsonArray:
                    case JsonBoolean jsonBoolean:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(node));
                }
        }

        throw new Exception();
    }

    public static string GetName(this TypeName typeName)
    {
        return $"{typeName.NameSpace}.{typeName.Name}";
    }

    private static string ToCamelCase(this string name)
    {
        return name.Substring(0, 1).ToUpper() + name.Substring(1);
    }
}

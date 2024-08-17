using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.NameResolve;

public record struct TypeName(string Name, string ModuleName);

public class NameTable(Dictionary<ITypeId, TypeName> typeNames, Dictionary<PropertyId, string> propertyNames)
{
    public readonly Dictionary<PropertyId, string> PropertyNames = propertyNames;

//    public readonly Dictionary<string, Guid> Ids = names.ToDictionary(x => x.Value, x => x.Key);
    public readonly Dictionary<ITypeId, TypeName> TypeNames = typeNames;
}

public static class NameResolver
{
    public static NameTable Resolve(Semantics semantics, SchemaTable schemaTable)
    {
        var typeNames = new Dictionary<ITypeId, string>();
        var propertyNames = new Dictionary<PropertyId, string>();

        // root以外の全てのtypeの名前を登録
        foreach (var kvp in semantics.TypeSemanticsTable)
        {
            var id = kvp.Key;
            var typeSemantics = kvp.Value!;

            if (typeSemantics.Schema.Parent is null) continue;
            var name = schemaTable.Table[typeSemantics.Schema.Parent!.Value] switch
            {
                ObjectSchema => typeSemantics.Schema.PropertyName!,
                ArraySchema arraySchema => arraySchema.GetPropertyName(),
                //ArraySchema arraySchema => $"{arraySchema.PropertyName!}Element",
                OneOfSchema oneOfSchema => GetIfThenName(oneOfSchema.IfThenArray.ToDictionary(ifThen => schemaTable.Table[ifThen.Then])[typeSemantics.Schema]),
                _ => null
            };

            if (name is not null) typeNames[id] = name.ToCamelCase();
        }

        // rootのtypeの名前を登録
        foreach (var kvp in semantics.RootSemanticsTable)
        {
            var typeId = kvp.Value.ClassId;
            var root = kvp.Value!;

            typeNames[typeId] = root.Root.SchemaId.ToCamelCase();
        }

        // interfaceの名前を登録
        foreach (var kvp in semantics.InterfaceSemanticsTable)
        {
            var id = kvp.Key;
            var interfaceSemantics = kvp.Value!;

            var interfaceName = interfaceSemantics.Schema.PropertyName?.ToCamelCase();
            typeNames[id] = $"I{interfaceName}";
        }

        // namespaceを登録
        var nameSpaces = new Dictionary<ITypeId, string>();
        var schemaToRoot = semantics.RootSemanticsTable.ToDictionary(r => semantics.TypeSemanticsTable[r.Value.ClassId].Schema, r => r.Value);

        foreach (var typeId in typeNames.Keys)
        {
            // child → parent
            var parentNames = new List<string>();
            var schema = typeId switch
            {
                ClassId classId => semantics.TypeSemanticsTable[classId].Schema,
                InterfaceId interfaceId => semantics.InterfaceSemanticsTable[interfaceId].Schema,
                _ => throw new ArgumentOutOfRangeException(nameof(typeId))
            };

            var parentSchema = schema.Parent;
            var currentSchema = parentSchema;
            while (currentSchema is not null)
            {
                if (schemaTable.Table[currentSchema.Value].Parent is null)
                {
                    parentNames.Add(schemaToRoot[schemaTable.Table[currentSchema.Value]].Root.SchemaId);
                    break;
                }

                switch (schemaTable.Table[currentSchema.Value])
                {
                    case ArraySchema arraySchema:
                        if (parentNames.Count != 0) parentNames[parentNames.Count - 1] = arraySchema.GetPropertyName();
                        //if (parentNames.Count != 0) parentNames[parentNames.Count - 1] = $"{arraySchema.PropertyName}Element";

                        parentNames.Add(arraySchema.PropertyName);
                        currentSchema = arraySchema.Parent;
                        break;
                    case ObjectSchema objectSchema:
                        parentNames.Add(objectSchema.PropertyName);
                        currentSchema = objectSchema.Parent;
                        break;
                    case OneOfSchema oneOfSchema:
                        parentNames.Add(oneOfSchema.PropertyName);
                        currentSchema = oneOfSchema.Parent;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(currentSchema));
                }
            }

            //一応残しておく nameSpaces[typeId] = $"mooresmaster{string.Join("", ((IEnumerable<string>)parentNames).Reverse().Select(n => $".{n}Module"))}";
            var lastName = (0 < parentNames.Count ? parentNames[parentNames.Count - 1] : schema.PropertyName ?? schemaToRoot[schema].Root.SchemaId).ToCamelCase();
            lastName = $"{lastName}Module";

            nameSpaces[typeId] = lastName;
        }

        // propertyの名前を登録
        foreach (var kvp in semantics.PropertySemanticsTable)
        {
            var propertySemantics = kvp.Value;
            var name = propertySemantics.PropertyName.ToCamelCase();
            propertyNames[kvp.Key] = name;
        }

        return new NameTable(
            typeNames
                .Select(name =>
                    new KeyValuePair<ITypeId, TypeName>(
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
                    case JsonArray:
                    case JsonBoolean:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(node));
                }
        }

        throw new Exception();
    }

    public static string GetModelName(this TypeName typeName)
    {
        return $"Mooresmaster.Model.{typeName.ModuleName}.{typeName.Name}";
    }

    public static string GetLoaderName(this TypeName typeName)
    {
        return $"Mooresmaster.Loader.{typeName.ModuleName}.{typeName.Name}Loader";
    }

    private static string ToCamelCase(this string name)
    {
        return name.Substring(0, 1).ToUpper() + name.Substring(1);
    }
}

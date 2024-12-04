using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.NameResolve;

public record struct TypeName(string Name, string ModuleName);

public class NameTable(Dictionary<ITypeId, TypeName> typeNames, Dictionary<InterfacePropertyId, string> interfacePropertyNames, Dictionary<PropertyId, string> propertyNames)
{
    public readonly Dictionary<InterfacePropertyId, string> InterfacePropertyNames = interfacePropertyNames;
    public readonly Dictionary<PropertyId, string> PropertyNames = propertyNames;

    public readonly Dictionary<ITypeId, TypeName> TypeNames = typeNames;
}

public static class NameResolver
{
    public static NameTable Resolve(Semantics semantics, SchemaTable schemaTable)
    {
        var typeNames = new Dictionary<ITypeId, string>();
        var propertyNames = new Dictionary<PropertyId, string>();
        var interfacePropertyNames = new Dictionary<InterfacePropertyId, string>();

        // root以外の全てのtypeの名前を登録
        foreach (var kvp in semantics.TypeSemanticsTable)
        {
            var id = kvp.Key;
            var typeSemantics = kvp.Value!;
            Console.WriteLine(id);

            if (typeSemantics.Schema.Parent is null)
            {
                Console.WriteLine($"isRoot: {id}");
                continue;
            }

            Console.WriteLine(id);
            var name = schemaTable.Table[typeSemantics.Schema.Parent!.Value] switch
            {
                ObjectSchema => typeSemantics.Schema.PropertyName!,
                ArraySchema arraySchema => arraySchema.GetPropertyName(),
                SwitchSchema oneOfSchema => GetIfThenName(oneOfSchema, schemaTable, typeSemantics),
                _ => null
            };

            if (name is not null) typeNames[id] = name.ToCamelCase();
            else Console.WriteLine($"is null: {id}");
        }

        // rootのtypeの名前を登録
        foreach (var kvp in semantics.RootSemanticsTable)
        {
            var typeId = kvp.Value.ClassId;
            var root = kvp.Value!;

            typeNames[typeId] = root.Root.SchemaId.ToCamelCase();
        }

        // switchの名前を登録
        foreach (var kvp in semantics.SwitchSemanticsTable)
        {
            var id = kvp.Key;
            var interfaceSemantics = kvp.Value!;

            var interfaceName = interfaceSemantics.Schema.PropertyName?.ToCamelCase();
            typeNames[id] = $"I{interfaceName}";
        }

        // interfaceの名前を登録
        foreach (var kvp in semantics.InterfaceSemanticsTable)
        {
            var id = kvp.Key;
            var interfaceSemantics = kvp.Value;

            var interfaceName = interfaceSemantics.Interface.InterfaceName.ToCamelCase();
            typeNames[id] = interfaceName;
        }

        // namespaceを登録
        var nameSpaces = new Dictionary<ITypeId, string>();
        var schemaToRoot = semantics.RootSemanticsTable.ToDictionary(r => semantics.TypeSemanticsTable[r.Value.ClassId].Schema, r => r.Value);

        foreach (var typeId in typeNames.Keys)
        {
            if (typeId is InterfaceId interfaceId)
            {
                nameSpaces[typeId] = $"{semantics.InterfaceSemanticsTable[interfaceId].Schema.SchemaId.ToCamelCase()}Module";
                continue;
            }

            // child → parent
            var parentNames = new List<string>();
            var schema = typeId switch
            {
                ClassId classId => semantics.TypeSemanticsTable[classId].Schema,
                SwitchId switchId => semantics.SwitchSemanticsTable[switchId].Schema,
                _ => throw new ArgumentOutOfRangeException(typeId.GetType().Name)
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

                        parentNames.Add(arraySchema.PropertyName);
                        currentSchema = arraySchema.Parent;
                        break;
                    case ObjectSchema objectSchema:
                        parentNames.Add(objectSchema.PropertyName);
                        currentSchema = objectSchema.Parent;
                        break;
                    case SwitchSchema oneOfSchema:
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

        // interfacePropertyの名前を登録
        foreach (var kvp in semantics.InterfacePropertySemanticsTable)
        {
            var interfacePropertyId = kvp.Key;
            var schema = kvp.Value.PropertySchema;
            var name = schema.PropertyName!.ToCamelCase();
            interfacePropertyNames[interfacePropertyId] = name;
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
            interfacePropertyNames,
            propertyNames
        );
    }

    private static string GetIfThenName(SwitchSchema switchSchema, SchemaTable schemaTable, TypeSemantics typeSemantics)
    {
        var ifThenSchema = switchSchema.IfThenArray.ToDictionary(ifThen => schemaTable.Table[ifThen.Then])[typeSemantics.Schema];
        return $"{ifThenSchema.If.Literal}{switchSchema.PropertyName?.ToCamelCase()}";
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

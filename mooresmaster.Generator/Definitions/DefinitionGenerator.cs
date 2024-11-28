using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.NameResolve;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Definitions;

public static class DefinitionGenerator
{
    public static Definition Generate(Semantics semantics, NameTable nameTable, SchemaTable schemaTable)
    {
        var definition = new Definition();

        foreach (var interfaceSemantics in semantics.InterfaceSemanticsTable)
        {
            // プロパティを生成
            var propertyTable = new Dictionary<string, InterfacePropertyDefinition>();
            foreach (var propertyId in interfaceSemantics.Value.Properties)
            {
                var interfaceProperty = semantics.InterfacePropertySemanticsTable[propertyId];
                var typeId = propertyId;
                var name = nameTable.InterfacePropertyNames[propertyId];

                var type = Type.GetType(nameTable, typeId, interfaceProperty.PropertySchema, semantics, schemaTable);

                propertyTable[name] = new InterfacePropertyDefinition(
                    type
                );
            }

            // Implementationを取得
            var implementations = new List<TypeName>();
            if (semantics.InterfaceImplementationTable.TryGetValue(interfaceSemantics.Key, out var implementationsList))
                foreach (var implementationInterfaceId in implementationsList)
                {
                    var implementationInterface = nameTable.TypeNames[implementationInterfaceId];
                    implementations.Add(implementationInterface);
                }

            definition.InterfaceDefinitions.Add(new InterfaceDefinition(
                    $"mooresmaster.{nameTable.TypeNames[interfaceSemantics.Key].Name}.g.cs",
                    nameTable.TypeNames[interfaceSemantics.Key],
                    propertyTable,
                    implementations
                )
            );
        }

        foreach (var switchSemantics in semantics.SwitchSemanticsTable)
            definition.InterfaceDefinitions.Add(new InterfaceDefinition(
                    $"mooresmaster.{nameTable.TypeNames[switchSemantics.Key].Name}.g.cs",
                    nameTable.TypeNames[switchSemantics.Key],
                    [],
                    []
                )
            );
        var inheritTable = new Dictionary<ITypeId, List<SwitchId>>();
        foreach (var inherit in semantics.SwitchInheritList)
        {
            if (!inheritTable.TryGetValue(inherit.typeId, out var interfaceList))
            {
                inheritTable[inherit.typeId] = [];
                interfaceList = inheritTable[inherit.typeId];
            }

            interfaceList.Add(inherit.switchId);
        }

        var schemaToRootTable = semantics.RootSemanticsTable.ToDictionary(kvp => schemaTable.Table[kvp.Value.Root.InnerSchema], kvp => kvp.Value.Root);

        foreach (var kvp in semantics.TypeSemanticsTable)
        {
            var typeId = kvp.Key;
            var typeSemantics = kvp.Value!;

            var isInherited = inheritTable.TryGetValue(typeId, out var interfaceList);
            var typeName = nameTable.TypeNames[typeId];
            var inheritList = isInherited ? interfaceList!.Select(i => nameTable.TypeNames[i]).ToArray() : [];
            var propertyTable = GetProperties(nameTable, typeId, semantics, schemaTable);

            var fileName = "mooresmaster.g.cs";
            if (inheritList.Length == 0)
            {
                var rootSchema = typeSemantics.Schema;
                while (rootSchema.Parent != null) rootSchema = schemaTable.Table[rootSchema.Parent.Value];

                var root = schemaToRootTable[rootSchema];
                var schemaId = root.SchemaId;
                fileName = $"mooresmaster.{schemaId}.g.cs";
            }
            else
            {
                var firstInterface = inheritList[0];
                fileName = $"mooresmaster.{firstInterface.Name}.g.cs";
            }

            definition.TypeDefinitions.Add(new TypeDefinition(fileName, typeName, inheritList, propertyTable));
        }

        return definition;
    }

    private static Dictionary<string, PropertyDefinition> GetProperties(NameTable nameTable, ClassId classId, Semantics semantics, SchemaTable table)
    {
        var propertyTable = new Dictionary<string, PropertyDefinition>();
        var typeSemantics = semantics.TypeSemanticsTable[classId]!;

        switch (typeSemantics.Schema)
        {
            case ArraySchema arraySchema:
                propertyTable["items"] = new PropertyDefinition(new ArrayType(Type.GetType(nameTable, semantics.SchemaTypeSemanticsTable[table.Table[arraySchema.Items]], table.Table[arraySchema.Items], semantics, table)), null, arraySchema.IsNullable, null);
                break;
            case BooleanSchema:
                propertyTable["value"] = new PropertyDefinition(new BooleanType(), null, typeSemantics.Schema.IsNullable, null);
                break;
            case IntegerSchema:
                propertyTable["value"] = new PropertyDefinition(new IntType(), null, typeSemantics.Schema.IsNullable, null);
                break;
            case NumberSchema:
                propertyTable["value"] = new PropertyDefinition(new FloatType(), null, typeSemantics.Schema.IsNullable, null);
                break;
            case StringSchema stringSchema:
                propertyTable["value"] = new PropertyDefinition(new StringType(), null, typeSemantics.Schema.IsNullable, stringSchema.Enums);
                break;
            case ObjectSchema:
                foreach (var propertyId in typeSemantics.Properties)
                {
                    var propertySemantics = semantics.PropertySemanticsTable[propertyId];
                    var propertyTypeId = propertySemantics.PropertyType;
                    var schema = semantics.PropertySemanticsTable[propertyId].Schema;
                    var name = nameTable.PropertyNames[propertyId];
                    string[]? enums = null;
                    if (schema is StringSchema stringSchema) enums = stringSchema.Enums;

                    propertyTable[name] = new PropertyDefinition(Type.GetType(nameTable, propertyTypeId, schema, semantics, table), propertyId, typeSemantics.Schema.IsNullable, enums);
                }

                break;
            case SwitchSchema:
                propertyTable["value"] = new PropertyDefinition(new CustomType(nameTable.TypeNames[classId]), null, typeSemantics.Schema.IsNullable, null);
                break;
            case RefSchema refSchema:
                propertyTable["value"] = new PropertyDefinition(new CustomType(refSchema.GetRefName()), null, typeSemantics.Schema.IsNullable, null);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(typeSemantics.Schema));
        }

        return propertyTable;
    }
}

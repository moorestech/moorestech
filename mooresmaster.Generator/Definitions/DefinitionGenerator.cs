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
        var definitions = new Definition();

        foreach (var interfaceSemantics in semantics.InterfaceSemanticsTable)
            definitions.InterfaceDefinitions.Add(
                new InterfaceDefinition(
                    $"mooresmaster.{nameTable.TypeNames[interfaceSemantics.Key].Name}.g.cs",
                    nameTable.TypeNames[interfaceSemantics.Key]
                )
            );
        var inheritTable = new Dictionary<ITypeId, List<InterfaceId>>();
        foreach (var inherit in semantics.InheritList)
        {
            if (!inheritTable.TryGetValue(inherit.typeId, out var interfaceList))
            {
                inheritTable[inherit.typeId] = [];
                interfaceList = inheritTable[inherit.typeId];
            }

            interfaceList.Add(inherit.interfaceId);
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

            definitions.TypeDefinitions.Add(new TypeDefinition(fileName, typeName, inheritList, propertyTable));
        }

        return definitions;
    }

    private static Dictionary<string, Type> GetProperties(NameTable nameTable, ClassId classId, Semantics semantics, SchemaTable table)
    {
        var propertyTable = new Dictionary<string, Type>();
        var typeSemantics = semantics.TypeSemanticsTable[classId]!;

        switch (typeSemantics.Schema)
        {
            case ArraySchema arraySchema:
                propertyTable["items"] = new ArrayType(Type.GetType(nameTable, semantics.SchemaTypeSemanticsTable[table.Table[arraySchema.Items]], table.Table[arraySchema.Items], semantics, table));
                break;
            case BooleanSchema:
                propertyTable["value"] = new BooleanType();
                break;
            case IntegerSchema:
                propertyTable["value"] = new IntType();
                break;
            case NumberSchema:
                propertyTable["value"] = new FloatType();
                break;
            case StringSchema:
                propertyTable["value"] = new StringType();
                break;
            case ObjectSchema objectSchema:
                foreach (var propertyId in typeSemantics.Properties)
                {
                    var propertySemantics = semantics.PropertySemanticsTable[propertyId];
                    var propertyTypeId = propertySemantics.PropertyType;
                    var schema = semantics.PropertySemanticsTable[propertyId].Schema;
                    var name = nameTable.PropertyNames[propertyId];
                    propertyTable[name] = Type.GetType(nameTable, propertyTypeId, schema, semantics, table);
                }

                break;
            case OneOfSchema:
                propertyTable["value"] = new CustomType(nameTable.TypeNames[classId].GetName());
                break;
            case RefSchema refSchema:
                propertyTable["value"] = new CustomType(refSchema.Ref);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(typeSemantics.Schema));
        }

        return propertyTable;
    }
}

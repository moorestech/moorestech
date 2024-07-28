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

        foreach (var interfaceSemantics in semantics.InterfaceSemanticsTable) definitions.InterfaceDefinitions.Add(new InterfaceDefinition(nameTable.Names[interfaceSemantics.Key]));
        var inheritTable = new Dictionary<Guid, List<Guid>>();
        foreach (var inherit in semantics.InheritList)
        {
            if (!inheritTable.TryGetValue(inherit.typeId, out var interfaceList))
            {
                inheritTable[inherit.typeId] = [];
                interfaceList = inheritTable[inherit.typeId];
            }

            interfaceList.Add(inherit.interfaceId);
        }

        foreach (var kvp in semantics.TypeSemanticsTable)
        {
            var id = kvp.Key;
            var typeSemantics = kvp.Value!;

            var isInherited = inheritTable.TryGetValue(id, out var interfaceList);
            var typeName = nameTable.Names[id];
            var inheritList = isInherited ? interfaceList!.Select(i => nameTable.Names[i]).ToArray() : [];
            var propertyTable = GetProperties(nameTable, id, typeSemantics, schemaTable);

            definitions.TypeDefinitions.Add(new TypeDefinition(typeName, inheritList, propertyTable));
        }

        return definitions;
    }

    private static Dictionary<string, Type> GetProperties(NameTable nameTable, Guid id, TypeSemantics typeSemantics, SchemaTable table)
    {
        var propertyTable = new Dictionary<string, Type>();

        switch (typeSemantics.Schema)
        {
            case ArraySchema arraySchema:
                propertyTable["items"] = new ArrayType(Type.GetType(nameTable, id, table.Table[arraySchema.Items], table));
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
                foreach (var (name, propertyTypeId) in typeSemantics.Properties) propertyTable[name] = Type.GetType(nameTable, propertyTypeId, table.Table[objectSchema.Properties[name]], table);
                break;
            case OneOfSchema:
                propertyTable["value"] = new CustomType(nameTable.Names[id].GetName());
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

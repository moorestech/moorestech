using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.NameResolve;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Definitions;

public static class DefinitionGenerator
{
    public static Definition Generate(Semantics semantics, NameTable nameTable)
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
        
        foreach (var typeSemantics in semantics.TypeSemanticsTable)
        {
            var isInherited = inheritTable.TryGetValue(typeSemantics.Key, out var interfaceList);
            var typeName = nameTable.Names[typeSemantics.Key];
            var inheritList = isInherited ? interfaceList!.Select(i => nameTable.Names[i]).ToArray() : [];
            var propertyTable = GetProperties(nameTable, typeSemantics.Key, typeSemantics.Value.Schema);
            
            definitions.TypeDefinitions.Add(new TypeDefinition(typeName, inheritList, propertyTable));
        }
        
        return definitions;
    }
    
    private static Dictionary<string, Type> GetProperties(NameTable nameTable, Guid id, ISchema schema)
    {
        var propertyTable = new Dictionary<string, Type>();
        
        switch (schema)
        {
            case ArraySchema arraySchema:
                propertyTable["items"] = new ArrayType(Type.GetType(nameTable, id, arraySchema.Items));
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
                foreach (var kvp in objectSchema.Properties) propertyTable[kvp.Key] = Type.GetType(nameTable, nameTable.Ids[kvp.Key], kvp.Value);
                break;
            case OneOfSchema:
                propertyTable["value"] = new CustomType(nameTable.Names[id]);
                break;
            case RefSchema refSchema:
                propertyTable["value"] = new CustomType(refSchema.Ref);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(schema));
        }
        
        return propertyTable;
    }
}

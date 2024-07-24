using System;
using System.Collections.Generic;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Definitions;

public static class DefinitionGenerator
{
    public static Definition Generate(Semantics semantics)
    {
        var definitions = new Definition();
        
        foreach (var interfaceSemantics in semantics.InterfaceSemantics.Values) definitions.InterfaceDefinitions.Add(new InterfaceDefinition(interfaceSemantics.Name));
        var inheritTable = new Dictionary<string, List<string>>();
        foreach (var inherit in semantics.InheritList)
        {
            if (!inheritTable.TryGetValue(inherit.typeName, out var interfaceList))
            {
                inheritTable[inherit.typeName] = [];
                interfaceList = inheritTable[inherit.typeName];
            }
            
            interfaceList.Add(inherit.interfaceName);
        }
        
        foreach (var typeSemantics in semantics.TypeSemantics.Values)
        {
            var isInherited = inheritTable.TryGetValue(typeSemantics.Name, out var interfaceList);
            var typeName = typeSemantics.Name;
            var inheritList = isInherited ? interfaceList!.ToArray() : [];
            var propertyTable = GetProperties(semantics, typeSemantics.Schema);
            
            definitions.TypeDefinitions.Add(new TypeDefinition(typeName, inheritList, propertyTable));
        }
        
        return definitions;
    }
    
    private static Dictionary<string, Type> GetProperties(Semantics semantics, ISchema schema)
    {
        var propertyTable = new Dictionary<string, Type>();
        
        switch (schema)
        {
            case ArraySchema arraySchema:
                propertyTable["items"] = new ArrayType(Type.GetType(semantics, arraySchema.Items));
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
                foreach (var kvp in objectSchema.Properties) propertyTable[kvp.Key] = Type.GetType(semantics, kvp.Value);
                break;
            case OneOfSchema oneOfSchema:
                propertyTable["value"] = new CustomType(semantics.OneOfToInterface[oneOfSchema]);
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

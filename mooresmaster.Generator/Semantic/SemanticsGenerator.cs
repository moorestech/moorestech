using System;
using System.Collections.Immutable;
using System.Linq;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Semantic;

public static class SemanticsGenerator
{
    public static Semantics Generate(ImmutableArray<Schema> schemaArray)
    {
        var semantics = new Semantics();
        
        foreach (var schema in schemaArray)
        {
            // ファイルに分けられているルートの要素はclassになる
            semantics.TypeSemantics[schema.Id] = new TypeSemantics(schema.Id, schema.InnerSchema);
            
            Generate(schema.Id, schema.InnerSchema).AddTo(semantics);
        }
        
        return semantics;
    }
    
    private static Semantics Generate(string name, ISchema schema)
    {
        var semantics = new Semantics();
        
        switch (schema)
        {
            case ObjectSchema objectSchema:
                semantics.ObjectSchemaToType[objectSchema] = name;
                semantics.TypeSemantics[name] = new TypeSemantics(name, schema);
                foreach (var property in objectSchema.Properties) Generate(property.Key, property.Value).AddTo(semantics);
                break;
            case ArraySchema arraySchema:
                Generate($"{name}Element", arraySchema.Items).AddTo(semantics);
                break;
            case OneOfSchema oneOfSchema:
                // InterfaceSemanticsを生成
                var interfaceName = $"I{name}";
                var interfaceSemantics = new InterfaceSemantics(interfaceName, oneOfSchema);
                semantics.OneOfToInterface[oneOfSchema] = interfaceName;
                semantics.InterfaceSemantics[interfaceName] = interfaceSemantics;
                // 全ての可能性の型を生成
                for (var i = 0; i < oneOfSchema.IfThenArray.Length; i++)
                {
                    var ifThen = oneOfSchema.IfThenArray[i];
                    var typeName = GetInheritedTypeName(ifThen.If) ?? $"{interfaceSemantics}{i}";
                    
                    semantics.InheritList.Add((interfaceName, typeName));
                    Generate(typeName, ifThen.Then).AddTo(semantics);
                }
                
                break;
            case RefSchema:
            case IntegerSchema:
            case NumberSchema:
            case BooleanSchema:
            case StringSchema:
                // ignore
                break;
            default:
                throw new NotImplementedException($"not implemented: {name} {schema.GetType()}");
        }
        
        return semantics;
    }
    
    private static string? GetInheritedTypeName(IJsonNode? json)
    {
        if (json is null) return null;
        
        switch (json)
        {
            case JsonObject jsonObject:
                foreach (var name in jsonObject.Nodes.Select(node => GetInheritedTypeName(node.Value)).OfType<string>()) return name;
                break;
            case JsonArray jsonArray:
                foreach (var name in jsonArray.Nodes.Select(GetInheritedTypeName).OfType<string>()) return name;
                break;
            case JsonString jsonString:
                return jsonString.Literal;
            case JsonBoolean:
                return null;
        }
        
        return null;
    }
}

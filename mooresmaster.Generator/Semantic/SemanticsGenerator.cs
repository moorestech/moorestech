using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
            var typeSemantics = new TypeSemantics(null, [], schema.InnerSchema);
            var typeId = semantics.AddTypeSemantics(typeSemantics);
            semantics.AddRootSemantics(new RootSemantics(schema, typeId));
            
            Generate(typeId, schema.InnerSchema).AddTo(semantics);
        }
        
        return semantics;
    }
    
    private static Semantics Generate(Guid parentTypeId, ISchema schema)
    {
        var semantics = new Semantics();
        
        switch (schema)
        {
            case ArraySchema arraySchema:
                Generate(parentTypeId, arraySchema.Items).AddTo(semantics);
                break;
            case ObjectSchema objectSchema:
                break;
            case OneOfSchema oneOfSchema:
                semantics.AddInterfaceSemantics(new InterfaceSemantics(parentTypeId, oneOfSchema));
                foreach (var ifThen in oneOfSchema.IfThenArray) Generate(parentTypeId, ifThen.Then).AddTo(semantics);
                break;
            case RefSchema refSchema:
            case BooleanSchema booleanSchema:
            case IntegerSchema integerSchema:
            case NumberSchema numberSchema:
            case StringSchema stringSchema:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(schema));
        }
        
        return semantics;
    }
    
    private static (Semantics, Guid) Generate(Guid parentTypeId, ObjectSchema objectSchema)
    {
        var semantics = new Semantics();
        var typeId = Guid.NewGuid();
        var properties = new List<(string PropertyName, Guid? PropertyType)>();
        foreach (var property in objectSchema.Properties)
            if (property.Value is ObjectSchema innerObjectSchema)
            {
                var (innerSemantics, innerTypeId) = Generate(typeId, innerObjectSchema);
                innerSemantics.AddTo(semantics);
                properties.Add((property.Key, innerTypeId));
            }
            else
            {
                Generate(typeId, property.Value);
                properties.Add((property.Key, null));
            }
        
        semantics.TypeSemanticsTable[typeId] = new TypeSemantics(parentTypeId, properties.ToArray(), objectSchema);
        
        return (semantics, typeId);
    }
}

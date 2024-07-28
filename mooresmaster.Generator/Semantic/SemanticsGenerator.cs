using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Semantic;

public static class SemanticsGenerator
{
    public static Semantics Generate(ImmutableArray<Schema> schemaArray, SchemaTable table)
    {
        var semantics = new Semantics();
        
        foreach (var schema in schemaArray)
        {
            // ファイルに分けられているルートの要素はclassになる
            var typeSemantics = new TypeSemantics(null, [], table.Table[schema.InnerSchema]);
            var typeId = semantics.AddTypeSemantics(typeSemantics);
            semantics.AddRootSemantics(new RootSemantics(schema, typeId));
            
            Generate(typeId, table.Table[schema.InnerSchema], table).AddTo(semantics);
        }
        
        return semantics;
    }
    
    private static Semantics Generate(Guid parentTypeId, ISchema schema, SchemaTable table)
    {
        var semantics = new Semantics();
        
        switch (schema)
        {
            case ArraySchema arraySchema:
                Generate(parentTypeId, table.Table[arraySchema.Items], table).AddTo(semantics);
                break;
            case ObjectSchema objectSchema:
                var (innerSemantics, _) = Generate(parentTypeId, objectSchema, table);
                innerSemantics.AddTo(semantics);
                break;
            case OneOfSchema oneOfSchema:
                semantics.AddInterfaceSemantics(new InterfaceSemantics(parentTypeId, oneOfSchema));
                foreach (var ifThen in oneOfSchema.IfThenArray) Generate(parentTypeId, table.Table[ifThen.Then], table).AddTo(semantics);
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
    
    private static (Semantics, Guid) Generate(Guid parentTypeId, ObjectSchema objectSchema, SchemaTable table)
    {
        var semantics = new Semantics();
        var typeId = Guid.NewGuid();
        var properties = new List<(string PropertyName, Guid? PropertyType)>();
        foreach (var property in objectSchema.Properties)
            if (table.Table[property.Value] is ObjectSchema innerObjectSchema)
            {
                var (innerSemantics, innerTypeId) = Generate(typeId, innerObjectSchema, table);
                innerSemantics.AddTo(semantics);
                properties.Add((property.Key, innerTypeId));
            }
            else
            {
                Generate(typeId, table.Table[property.Value], table);
                properties.Add((property.Key, null));
            }
        
        var typeSemantics = new TypeSemantics(parentTypeId, properties.ToArray(), objectSchema);
        semantics.TypeSemanticsTable[typeId] = typeSemantics;
        semantics.SchemaTypeSemanticsTable[typeSemantics.Schema] = typeId;
        
        return (semantics, typeId);
    }
}

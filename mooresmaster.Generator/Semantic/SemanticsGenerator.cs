using System;
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
            var typeSemantics = new TypeSemantics(schema.InnerSchema);
            var typeId = semantics.AddTypeSemantics(typeSemantics);
            semantics.AddRootSemantics(new RootSemantics(schema, typeId));
            
            Generate(schema.InnerSchema);
        }
        
        return semantics;
    }
    
    private static Semantics Generate(ISchema schema)
    {
        var semantics = new Semantics();
        
        switch (schema)
        {
            case ArraySchema arraySchema:
                Generate(arraySchema.Items).AddTo(semantics);
                break;
            case ObjectSchema objectSchema:
                semantics.AddTypeSemantics(new TypeSemantics(objectSchema));
                foreach (var property in objectSchema.Properties) Generate(property.Value).AddTo(semantics);
                break;
            case OneOfSchema oneOfSchema:
                semantics.AddInterfaceSemantics(new InterfaceSemantics(oneOfSchema));
                foreach (var ifThen in oneOfSchema.IfThenArray) Generate(ifThen.Then).AddTo(semantics);
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
}

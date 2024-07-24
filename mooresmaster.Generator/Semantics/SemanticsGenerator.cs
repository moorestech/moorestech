using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Semantics;

public record CodeFile(string FileName, string Code)
{
    public string Code = Code;
    public string FileName = FileName;
}

public record TypeSemantics(string Name)
{
    public string Name;
}

public static class SemanticsGenerator
{
    public static TypeSemantics[] Generate(ImmutableArray<Schema> schemaArray)
    {
        var typeSemantics = new List<TypeSemantics>();
        
        foreach (var schema in schemaArray) typeSemantics.AddRange(Generate(schema));
        
        return typeSemantics.ToArray();
    }
    
    private static TypeSemantics[] Generate(Schema schema)
    {
        throw new NotImplementedException();
    }
}

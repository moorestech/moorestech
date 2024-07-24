using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantics;

namespace mooresmaster.Generator;

[Generator(LanguageNames.CSharp)]
public class SampleIncrementalSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.AdditionalTextsProvider.Collect(), Emit);
    }
    
    private void Emit(SourceProductionContext context, ImmutableArray<AdditionalText> additionalTexts)
    {
        var schemas = ParseAdditionalText(additionalTexts);
        var semantics = SemanticsGenerator.Generate(schemas.Select(schema => schema.Schema).ToImmutableArray());
        
        foreach (var schemaFile in schemas)
            context.AddSource(
                $"{Path.GetFileNameWithoutExtension(schemaFile.Path)}.g.cs",
                $$$"""
                   // Generate from "{{{Path.GetFileName(schemaFile.Path)}}}"
                   // ID: "{{{schemaFile.Schema.Id}}}"
                   """);
        
        foreach (var semantic in semantics.TypeSemantics) Console.WriteLine($"Type: {semantic.Value.Name} {semantic.Value.Schema.GetType().Name}");
        foreach (var semantic in semantics.InterfaceSemantics) Console.WriteLine($"Interface: {semantic.Value.Name} {semantic.Value.Schema.GetType().Name}");
    }
    
    private ImmutableArray<SchemaFile> ParseAdditionalText(ImmutableArray<AdditionalText> additionalTexts)
    {
        var schemas = new List<SchemaFile>();
        
        foreach (var additionalText in additionalTexts)
        {
            var text = additionalText.GetText()!.ToString();
            var json = JsonParser.Parse(JsonTokenizer.GetTokens(text));
            var schema = JsonSchemaParser.ParseSchema((json as JsonObject)!);
            schemas.Add(new SchemaFile(additionalText.Path, schema));
        }
        
        return schemas.ToImmutableArray();
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using mooresmaster.Generator.Definition;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantic;

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
        var definitions = DefinitionGenerator.Generate(semantics);
        
        foreach (var schemaFile in schemas)
            context.AddSource(
                $"{Path.GetFileNameWithoutExtension(schemaFile.Path)}.g.cs",
                $$$"""
                   // Generate from "{{{Path.GetFileName(schemaFile.Path)}}}"
                   // ID: "{{{schemaFile.Schema.Id}}}"
                   """);
        
        Console.WriteLine("Semantics: ");
        foreach (var semantic in semantics.TypeSemantics) Console.WriteLine($"    Type: {semantic.Value.Name} {semantic.Value.Schema.GetType().Name}");
        foreach (var semantic in semantics.InterfaceSemantics) Console.WriteLine($"    Interface: {semantic.Value.Name} {semantic.Value.Schema.GetType().Name}");
        foreach (var inherit in semantics.InheritList) Console.WriteLine($"    Inherit: {inherit.interfaceName} {inherit.typeName}");
        
        Console.WriteLine();
        
        Console.WriteLine("Definitions: ");
        foreach (var definition in definitions.TypeDefinitions)
        {
            Console.WriteLine($"    Type: {definition.Name} {definition.GetType().Name}");
            foreach (var inherit in definition.InheritList) Console.WriteLine($"        Inherit: {inherit}");
        }
        foreach (var definition in definitions.InterfaceDefinitions) Console.WriteLine($"    Interface: {definition.Name}");
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

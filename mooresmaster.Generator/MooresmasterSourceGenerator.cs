using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using mooresmaster.Generator.CodeGenerate;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.LoaderGenerate;
using mooresmaster.Generator.NameResolve;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator;

[Generator(LanguageNames.CSharp)]
public class MooresmasterSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additionalTextsProvider = context.AdditionalTextsProvider.Collect();
        var provider = context.CompilationProvider.Combine(additionalTextsProvider);
        context.RegisterSourceOutput(provider, Emit);
    }
    
    private void Emit(SourceProductionContext context, (Compilation compilation, ImmutableArray<AdditionalText> additionalTexts) input)
    {
        context.AddSource("mooresmaster.loader.BuiltinLoader.g.cs", LoaderGenerator.GenerateBuiltinLoaderCode());
        
        var (schemas, schemaTable) = ParseAdditionalText(input.additionalTexts);
        var semantics = SemanticsGenerator.Generate(schemas.Select(schema => schema.Schema).ToImmutableArray(), schemaTable);
        var nameTable = NameResolver.Resolve(semantics, schemaTable);
        var definitions = DefinitionGenerator.Generate(semantics, nameTable, schemaTable);
        
        foreach (var codeFile in CodeGenerator.Generate(definitions)) context.AddSource(codeFile.FileName, codeFile.Code);
        
        foreach (var loaderFile in LoaderGenerator.Generate(definitions, semantics, nameTable)) context.AddSource(loaderFile.FileName, loaderFile.Code);
    }
    
    private (ImmutableArray<SchemaFile> files, SchemaTable schemaTable) ParseAdditionalText(ImmutableArray<AdditionalText> additionalTexts)
    {
        var schemas = new List<SchemaFile>();
        var schemaTable = new SchemaTable();
        
        foreach (var additionalText in additionalTexts.Where(a => Path.GetExtension(a.Path) == ".json"))
        {
            var text = additionalText.GetText()!.ToString();
            var json = JsonParser.Parse(JsonTokenizer.GetTokens(text));
            var schema = JsonSchemaParser.ParseSchema((json as JsonObject)!, schemaTable);
            schemas.Add(new SchemaFile(additionalText.Path, schema));
        }
        
        foreach (var additionalText in additionalTexts.Where(a => Path.GetExtension(a.Path) == ".yml"))
        {
            var yamlText = additionalText.GetText()!.ToString();
            var jsonText = Yaml.ToJson(yamlText);
            var json = JsonParser.Parse(JsonTokenizer.GetTokens(jsonText));
            var schema = JsonSchemaParser.ParseSchema((json as JsonObject)!, schemaTable);
            schemas.Add(new SchemaFile(additionalText.Path, schema));
        }
        
        return (schemas.ToImmutableArray(), schemaTable);
    }
}

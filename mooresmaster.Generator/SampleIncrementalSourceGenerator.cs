using System.Collections.Generic;
using System.Collections.Immutable;
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
public class SampleIncrementalSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static c => c.AddSource("mooresmaster.loader.BuiltinLoader.g.cs", LoaderGenerator.GenerateBuiltinLoaderCode()));

        context.RegisterSourceOutput(context.AdditionalTextsProvider.Collect(), Emit);
    }

    private void Emit(SourceProductionContext context, ImmutableArray<AdditionalText> additionalTexts)
    {
        var (schemas, schemaTable) = ParseAdditionalText(additionalTexts);
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

        foreach (var additionalText in additionalTexts)
        {
            var text = additionalText.GetText()!.ToString();
            var json = JsonParser.Parse(JsonTokenizer.GetTokens(text));
            var schema = JsonSchemaParser.ParseSchema((json as JsonObject)!, schemaTable);
            schemas.Add(new SchemaFile(additionalText.Path, schema));
        }

        return (schemas.ToImmutableArray(), schemaTable);
    }
}

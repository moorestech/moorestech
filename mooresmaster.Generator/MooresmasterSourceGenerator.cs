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
        var (schemas, schemaTable) = ParseAdditionalText(input.additionalTexts);
        var semantics = SemanticsGenerator.Generate(schemas.Select(schema => schema.Schema).ToImmutableArray(), schemaTable);
        var nameTable = NameResolver.Resolve(semantics, schemaTable);
        var definitions = DefinitionGenerator.Generate(semantics, nameTable, schemaTable);

        var codeFiles = CodeGenerator.Generate(definitions);
        var loaderFiles = LoaderGenerator.Generate(definitions, semantics, nameTable);

        // 生成するファイルがある場合のみ固定生成コードを生成する
        if (codeFiles.Length == 0 && loaderFiles.Length == 0) return;

        foreach (var codeFile in codeFiles) context.AddSource(codeFile.FileName, codeFile.Code);
        foreach (var loaderFile in loaderFiles) context.AddSource(loaderFile.FileName, loaderFile.Code);

        context.AddSource("mooresmaster.loader.BuiltinLoader.g.cs", LoaderGenerator.GenerateBuiltinLoaderCode());
        context.AddSource("mooresmaster.loader.exception.g.cs", LoaderGenerator.GenerateLoaderExceptionTypeCode());
    }

    private (ImmutableArray<SchemaFile> files, SchemaTable schemaTable) ParseAdditionalText(ImmutableArray<AdditionalText> additionalTexts)
    {
        var schemas = new List<SchemaFile>();
        var schemaTable = new SchemaTable();

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

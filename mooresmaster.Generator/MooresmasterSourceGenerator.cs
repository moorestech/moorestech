using System;
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
        context.RegisterSourceOutput(provider, (sourceProductionContext, input) =>
        {
            try
            {
                Emit(sourceProductionContext, input);
            }
            catch (Exception e)
            {
                GenerateErrorFile(sourceProductionContext, e);
#pragma warning disable RS1035
                var environmentVariables = Environment.GetEnvironmentVariables() as Dictionary<string, string> ?? new Dictionary<string, string>();
                var isSourceGeneratorDebug = environmentVariables.TryGetValue("IsSourceGeneratorDebug", out var value) && value == "true";
#pragma warning restore RS1035
                if (isSourceGeneratorDebug) throw e;
            }
        });
    }
    
    private void GenerateErrorFile(SourceProductionContext context, Exception exception)
    {
        context.AddSource(
            "mooresmaster.error.g.cs",
            $$$"""
               // ErrorType:
               // {{{exception.GetType().Name}}}
               
               // Message: 
               // {{{
                   exception.Message
                       .Replace("\n", "\n// ")
               }}}
               
               // StackTrace:
               // {{{
                   exception.StackTrace
                       .Replace("\n", "\n// ")
               }}}
               """
        );
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
        
        context.AddSource(Tokens.BuiltinLoaderFileName, LoaderGenerator.GenerateBuiltinLoaderCode());
        context.AddSource(Tokens.ExceptionFileName, LoaderGenerator.GenerateLoaderExceptionTypeCode());
    }
    
    private (ImmutableArray<SchemaFile> files, SchemaTable schemaTable) ParseAdditionalText(ImmutableArray<AdditionalText> additionalTexts)
    {
        var schemas = new List<SchemaFile>();
        var schemaTable = new SchemaTable();
        var parsedFiles = new HashSet<string>();
        
        foreach (var additionalText in additionalTexts.Where(a => Path.GetExtension(a.Path) == ".yml").Where(a => !parsedFiles.Contains(a.Path)))
        {
            var yamlText = additionalText.GetText()!.ToString();
            var jsonText = Yaml.ToJson(yamlText);
            var json = JsonParser.Parse(JsonTokenizer.GetTokens(jsonText));
            var schema = JsonSchemaParser.ParseSchema((json as JsonObject)!, schemaTable);
            schemas.Add(new SchemaFile(additionalText.Path, schema));
            parsedFiles.Add(additionalText.Path);
        }
        
        return (schemas.ToImmutableArray(), schemaTable);
    }
}

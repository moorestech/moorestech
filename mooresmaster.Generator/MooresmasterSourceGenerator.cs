using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using mooresmaster.Generator.Analyze;
using mooresmaster.Generator.Analyze.Analyzers;
using mooresmaster.Generator.CodeGenerate;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.LoaderGenerate;
using mooresmaster.Generator.NameResolve;
using mooresmaster.Generator.Semantic;
using mooresmaster.Generator.Yaml;
using Enumerable = System.Linq.Enumerable;
using ImmutableArrayExtensions = System.Linq.ImmutableArrayExtensions;

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
                var isSourceGeneratorDebug = environmentVariables.TryGetValue(Tokens.IsSourceGeneratorDebug, out var value) && value == "true";
#pragma warning restore RS1035
                if (isSourceGeneratorDebug) throw e;
            }
        });
    }
    
    private void GenerateErrorFile(SourceProductionContext context, Exception exception)
    {
        context.AddSource(
            Tokens.ErrorFileName,
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
        var analyzer = new Analyzer()
            .AddAnalyzer(new DefineInterfaceScopeAnalyzer());
        
        var analysis = new Analysis();
        
        analyzer.PreJsonSchemaLayerAnalyze(analysis, input.additionalTexts.ToAnalyzerTextFiles());
        var (schemas, schemaTable) = ParseAdditionalText(input.additionalTexts);
        analyzer.PostJsonSchemaLayerAnalyze(analysis, schemas, schemaTable);
        
        analyzer.PreSemanticsLayerAnalyze(analysis, schemas, schemaTable);
        var semantics = SemanticsGenerator.Generate(ImmutableArrayExtensions.Select(schemas, schema => schema.Schema).ToImmutableArray(), schemaTable);
        analyzer.PostSemanticsLayerAnalyze(analysis, semantics, schemas, schemaTable);
        
        var nameTable = NameResolver.Resolve(semantics, schemaTable);
        analyzer.PreDefinitionLayerAnalyze(analysis, semantics, schemas, schemaTable);
        var definition = DefinitionGenerator.Generate(semantics, nameTable, schemaTable);
        analyzer.PostDefinitionLayerAnalyze(analysis, semantics, schemas, schemaTable, definition);
        
        var codeFiles = CodeGenerator.Generate(definition);
        var loaderFiles = LoaderGenerator.Generate(definition, semantics, nameTable);
        
        analysis.ThrowDiagnostics();
        
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
        
        foreach (var additionalText in Enumerable.Where(ImmutableArrayExtensions.Where(additionalTexts, a => Path.GetExtension(a.Path) == ".yml"), a => !parsedFiles.Contains(a.Path)))
        {
            var yamlText = additionalText.GetText()!.ToString();
            var json = YamlParser.Parse(additionalText.Path, yamlText);
            var schema = JsonSchemaParser.ParseSchema(json!, schemaTable);
            schemas.Add(new SchemaFile(additionalText.Path, schema));
            parsedFiles.Add(additionalText.Path);
        }
        
        return (schemas.ToImmutableArray(), schemaTable);
    }
}

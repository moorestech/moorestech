using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using mooresmaster.Generator.Analyze;
using mooresmaster.Generator.CodeGenerate;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.LoaderGenerate;
using mooresmaster.Generator.NameResolve;
using mooresmaster.Generator.Semantic;
using mooresmaster.Generator.Yaml;

namespace mooresmaster.Generator;

[Generator(LanguageNames.CSharp)]
public class MooresmasterSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additionalTextsProvider = context.AdditionalTextsProvider.Collect();
        var provider = context.CompilationProvider.Combine(additionalTextsProvider);
        var parseOptions = context.ParseOptionsProvider.Select((parseOptions, _) =>
        {
            if (parseOptions is CSharpParseOptions csharpParseOptions)
            {
                var hashset = new HashSet<string>();
                foreach (var define in csharpParseOptions.PreprocessorSymbolNames) hashset.Add(define);
                
                return hashset;
            }
            
            return [];
        });
        
        var withParseOptionsProvider = provider.Combine(parseOptions);
        context.RegisterSourceOutput(withParseOptionsProvider, (sourceProductionContext, input) =>
        {
            var inputCompilation = input.Left;
            var symbols = input.Right;
            
            if (!symbols.Contains("ENABLE_MOORESMASTER_GENERATOR")) return;
            
#pragma warning disable RS1035
            var environmentVariablesRaw = Environment.GetEnvironmentVariables();
            var environmentVariables = new Dictionary<string, string>();
            foreach (DictionaryEntry entry in environmentVariablesRaw) environmentVariables.Add(entry.Key.ToString()!, entry.Value.ToString()!);
            var isSourceGeneratorDebug = environmentVariables.TryGetValue(Tokens.IsSourceGeneratorDebug, out var value) && value == "true";
            if (isSourceGeneratorDebug)
                Emit(sourceProductionContext, inputCompilation);
            else
                try
                {
                    Emit(sourceProductionContext, inputCompilation);
                }
                catch (Exception e)
                {
                    GenerateErrorFile(sourceProductionContext, symbols, e);
                }
#pragma warning restore RS1035
        });
    }
    
    private void GenerateErrorFile(SourceProductionContext context, HashSet<string> symbols, Exception exception)
    {
        var errorFile =
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
               """;
        
        if (symbols.Contains("ENABLE_MOORESMASTER_ERROR_FILE_OUTPUT"))
        {
#pragma warning disable RS1035
            File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "mooresmaster_error.txt"), errorFile);
#pragma warning restore RS1035
        }
        
        context.AddSource(
            Tokens.ErrorFileName,
            errorFile
        );
    }
    
    private void Emit(SourceProductionContext context, (Compilation compilation, ImmutableArray<AdditionalText> additionalTexts) input)
    {
        var analyzer = new Analyzer()
            .AddAllAnalyzer();
        var analysis = new Analysis();
        analyzer.PreJsonSchemaLayerAnalyze(analysis, input.additionalTexts.ToAnalyzerTextFiles());
        var (schemas, schemaTable) = ParseAdditionalText(input.additionalTexts);
        analyzer.PostJsonSchemaLayerAnalyze(analysis, schemas, schemaTable);
        analyzer.PreSemanticsLayerAnalyze(analysis, schemas, schemaTable);
        var semantics = SemanticsGenerator.Generate(schemas.Select(schema => schema.Schema).ToImmutableArray(), schemaTable);
        analyzer.PostSemanticsLayerAnalyze(analysis, semantics, schemas, schemaTable);
        var nameTable = NameResolver.Resolve(semantics, schemaTable);
        analyzer.PreDefinitionLayerAnalyze(analysis, semantics, schemas, schemaTable);
        var definition = DefinitionGenerator.Generate(semantics, nameTable, schemaTable);
        analyzer.PostDefinitionLayerAnalyze(analysis, semantics, schemas, schemaTable, definition);
        
        var codeFiles = CodeGenerator.Generate(definition, semantics);
        var loaderFiles = LoaderGenerator.Generate(definition, semantics, nameTable);
        
        analysis.ReportCsDiagnostics(context);
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
        
        foreach (var additionalText in additionalTexts.Where(a => Path.GetExtension(a.Path) == ".yml").Where(a => !parsedFiles.Contains(a.Path)))
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
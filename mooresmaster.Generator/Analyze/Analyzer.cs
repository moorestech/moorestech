using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using mooresmaster.Generator.Analyze.Analyzers;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Analyze;

public class Analyzer
{
    private readonly List<IPostDefinitionLayerAnalyzer> _postDefinitionLayerAnalyzers = new();
    private readonly List<IPostJsonSchemaLayerAnalyzer> _postJsonSchemaLayerAnalyzers = new();
    private readonly List<IPostSemanticsLayerAnalyzer> _postSemanticsLayerAnalyzers = new();
    private readonly List<IPreDefinitionLayerAnalyzer> _preDefinitionLayerAnalyzers = new();
    private readonly List<IPreJsonSchemaLayerAnalyzer> _preJsonSchemaLayerAnalyzers = new();
    private readonly List<IPreSemanticsLayerAnalyzer> _preSemanticsLayerAnalyzers = new();
    
    public Analyzer AddAnalyzer(IAnalyzer analyzer)
    {
        switch (analyzer)
        {
            case IPostSemanticsLayerAnalyzer postSemanticsLayerAnalyzer:
                _postSemanticsLayerAnalyzers.Add(postSemanticsLayerAnalyzer);
                break;
            case IPreSemanticsLayerAnalyzer preSemanticsLayerAnalyzer:
                _preSemanticsLayerAnalyzers.Add(preSemanticsLayerAnalyzer);
                break;
            case IPostDefinitionLayerAnalyzer postDefinitionLayerAnalyzer:
                _postDefinitionLayerAnalyzers.Add(postDefinitionLayerAnalyzer);
                break;
            case IPreDefinitionLayerAnalyzer preDefinitionLayerAnalyzer:
                _preDefinitionLayerAnalyzers.Add(preDefinitionLayerAnalyzer);
                break;
            case IPostJsonSchemaLayerAnalyzer postJsonSchemaLayerAnalyzer:
                _postJsonSchemaLayerAnalyzers.Add(postJsonSchemaLayerAnalyzer);
                break;
            case IPreJsonSchemaLayerAnalyzer preJsonSchemaLayerAnalyzer:
                _preJsonSchemaLayerAnalyzers.Add(preJsonSchemaLayerAnalyzer);
                break;
        }
        
        return this;
    }
    
    public Analyzer AddAllAnalyzer()
    {
        IAnalyzer[] analyzers =
        [
            new DefineInterfaceScopeAnalyzer(),
            new SwitchPathAnalyzer(),
            new DuplicateImplementationInterfaceAnalyzer(),
            new DuplicateInterfaceNameAnalyzer()
        ];

        foreach (var analyzer in analyzers) AddAnalyzer(analyzer);

        return this;
    }
    
    public void PostJsonSchemaLayerAnalyze(Analysis analysis, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable)
    {
        foreach (var postJsonSchemaLayerAnalyzer in _postJsonSchemaLayerAnalyzers) postJsonSchemaLayerAnalyzer.PostJsonSchemaLayerAnalyze(analysis, schemaFiles, schemaTable);
    }
    
    public void PreJsonSchemaLayerAnalyze(Analysis analysis, AnalyzerTextFile[] texts)
    {
        foreach (var preJsonSchemaLayerAnalyzer in _preJsonSchemaLayerAnalyzers) preJsonSchemaLayerAnalyzer.PreJsonSchemaLayerAnalyze(analysis, texts);
    }
    
    public void PostSemanticsLayerAnalyze(Analysis analysis, Semantics semantics, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable)
    {
        foreach (var postSemanticsLayerAnalyzer in _postSemanticsLayerAnalyzers) postSemanticsLayerAnalyzer.PostSemanticsLayerAnalyze(analysis, semantics, schemaFiles, schemaTable);
    }
    
    public void PreSemanticsLayerAnalyze(Analysis analysis, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable)
    {
        foreach (var preSemanticsLayerAnalyzer in _preSemanticsLayerAnalyzers) preSemanticsLayerAnalyzer.PreSemanticsLayerAnalyze(analysis, schemaFiles, schemaTable);
    }
    
    public void PostDefinitionLayerAnalyze(Analysis analysis, Semantics semantics, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable, Definition definition)
    {
        foreach (var postDefinitionLayerAnalyzer in _postDefinitionLayerAnalyzers) postDefinitionLayerAnalyzer.PostDefinitionLayerAnalyze(analysis, semantics, schemaFiles, schemaTable, definition);
    }
    
    public void PreDefinitionLayerAnalyze(Analysis analysis, Semantics semantics, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable)
    {
        foreach (var preDefinitionLayerAnalyzer in _preDefinitionLayerAnalyzers) preDefinitionLayerAnalyzer.PreDefinitionLayerAnalyze(analysis, semantics, schemaFiles, schemaTable);
    }
}

public interface IAnalyzer;

public interface IPostJsonSchemaLayerAnalyzer : IAnalyzer
{
    void PostJsonSchemaLayerAnalyze(Analysis analysis, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable);
}

public interface IPreJsonSchemaLayerAnalyzer : IAnalyzer
{
    void PreJsonSchemaLayerAnalyze(Analysis analysis, AnalyzerTextFile[] texts);
}

public interface IPostSemanticsLayerAnalyzer : IAnalyzer
{
    void PostSemanticsLayerAnalyze(Analysis analysis, Semantics semantics, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable);
}

public interface IPreSemanticsLayerAnalyzer : IAnalyzer
{
    void PreSemanticsLayerAnalyze(Analysis analysis, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable);
}

public interface IPostDefinitionLayerAnalyzer : IAnalyzer
{
    void PostDefinitionLayerAnalyze(Analysis analysis, Semantics semantics, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable, Definition definition);
}

public interface IPreDefinitionLayerAnalyzer : IAnalyzer
{
    void PreDefinitionLayerAnalyze(Analysis analysis, Semantics semantics, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable);
}

public struct AnalyzerTextFile(string filePath, string text)
{
    public string FilePath = filePath;
    public string Text = text;
}

public static class TextFileExtension
{
    public static AnalyzerTextFile[] ToAnalyzerTextFiles(this ImmutableArray<AdditionalText> additionalTexts)
    {
        return additionalTexts.Select(t => new AnalyzerTextFile(t.Path, t.GetText()?.ToString() ?? "")).ToArray();
    }
    
    public static AnalyzerTextFile[] ToAnalyzerTextFiles(this string[] texts)
    {
        return texts.Select(t => new AnalyzerTextFile("", t)).ToArray();
    }
}
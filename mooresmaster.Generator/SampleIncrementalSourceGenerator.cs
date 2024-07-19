using System.IO;
using Microsoft.CodeAnalysis;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator;

[Generator(LanguageNames.CSharp)]
public class SampleIncrementalSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.AdditionalTextsProvider, Emit);
    }
    
    private void Emit(SourceProductionContext context, AdditionalText additionalText)
    {
        var text = additionalText.GetText()!.ToString();
        var commentedText = $"//{text.Replace("\n", "\n//")}";
        var json = JsonParser.Parse(JsonTokenizer.GetTokens(text));
        var schema = JsonSchemaParser.Parse((json as JsonObject)!);
        
        var code = $$$"""
                      // Generate from {{{Path.GetFileName(additionalText.Path)}}}
                      
                      {{{commentedText}}}
                      
                      // {{{json}}}
                      
                      // {{{schema}}}
                      """;
        
        context.AddSource($"{Path.GetFileNameWithoutExtension(additionalText.Path)}.g.cs", code);
    }
}

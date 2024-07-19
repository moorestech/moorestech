using System.IO;
using Microsoft.CodeAnalysis;
using mooresmaster.Generator.Json;

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
        var node = JsonParser.Parse(JsonTokenizer.GetTokens(text));
        
        var code = $$$"""
                      // Generate from {{{Path.GetFileName(additionalText.Path)}}}
                      
                      {{{commentedText}}}
                      
                      // {{{node}}}
                      """;
        
        context.AddSource($"{Path.GetFileNameWithoutExtension(additionalText.Path)}.g.cs", code);
    }
}

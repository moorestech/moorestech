using System.IO;
using Microsoft.CodeAnalysis;

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
        
        
        var code = $$$"""
                      // Generate from {{{Path.GetFileName(additionalText.Path)}}}
                      
                      {{{commentedText}}}
                      """;
        context.AddSource($"{Path.GetFileNameWithoutExtension(additionalText.Path)}.g.cs", code);
    }
}

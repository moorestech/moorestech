using System;
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
    
    private void Emit(SourceProductionContext context)
    {
        Console.WriteLine("Emit");
        context.AddSource("test.g.cs", "// Generate from test.g.cs");
    }
    
    private void Emit(SourceProductionContext context, AdditionalText text)
    {
        Console.WriteLine("AdditionalTextEmit");
        context.AddSource($"{Path.GetFileNameWithoutExtension(text.Path)}.g.cs", $"// Generate from {Path.GetFileName(text.Path)}");
    }
}

using System;
using System.IO;
using Microsoft.CodeAnalysis;

namespace mooresmaster.Generator;

[Generator(LanguageNames.CSharp)]
public class SampleIncrementalSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        Console.WriteLine("hoge");
        context.RegisterPostInitializationOutput(context => { context.AddSource("postinit.g.cs", "// Generate from postinit.g.cs"); });
        context.RegisterSourceOutput(
            context.SyntaxProvider.ForAttributeWithMetadataName(
                "global::mooresmaster.MoorestechJsonSchemaAttribute",
                static (_, _) => true,
                (productionContext, token) => productionContext
            ),
            (productionContext, syntaxContext) => Emit(productionContext)
        );
//        context.RegisterSourceOutput(context.AdditionalTextsProvider, Emit);
    }
    
    private void Emit(SourceProductionContext context)
    {
        context.AddSource("test.g.cs", "// Generate from test.g.cs");
    }
    
    private void Emit(SourceProductionContext context, AdditionalText text)
    {
        context.AddSource(Path.GetFileNameWithoutExtension(text.Path), $"// Generate from {Path.GetFileName(text.Path)}");
    }
}

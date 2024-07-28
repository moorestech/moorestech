using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using mooresmaster.Generator.Code;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.NameResolve;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator;

[Generator(LanguageNames.CSharp)]
public class SampleIncrementalSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.AdditionalTextsProvider.Collect(), Emit);
    }

    private void Emit(SourceProductionContext context, ImmutableArray<AdditionalText> additionalTexts)
    {
        var schemas = ParseAdditionalText(additionalTexts);
        var semantics = SemanticsGenerator.Generate(schemas.Select(schema => schema.Schema).ToImmutableArray());
        var nameTable = NameResolver.Resolve(semantics);
        var definitions = DefinitionGenerator.Generate(semantics, nameTable);

        Console.WriteLine("Semantics: ");
        foreach (var typeSemantic in semantics.TypeSemanticsTable) Console.WriteLine($"    Type: {nameTable.Names[typeSemantic.Key]} {typeSemantic.Value.Schema.GetType().Name}");
        foreach (var interfaceSemantics in semantics.InterfaceSemanticsTable) Console.WriteLine($"    Interface: {nameTable.Names[interfaceSemantics.Key]} {interfaceSemantics.Value.Schema.GetType().Name}");
        foreach (var inherit in semantics.InheritList) Console.WriteLine($"    Inherit: {nameTable.Names[inherit.typeId]} {nameTable.Names[inherit.typeId]}");

        Console.WriteLine();

        Console.WriteLine("Definitions: ");
        foreach (var definition in definitions.TypeDefinitions)
        {
            Console.WriteLine($"    Type: {definition.Name} {definition.GetType().Name}");
            if (definition.InheritList.Length > 0) Console.WriteLine("        Inherit:");
            foreach (var inherit in definition.InheritList) Console.WriteLine($"            {inherit}");
            if (definition.PropertyTable.Count > 0) Console.WriteLine("        Property:");
            foreach (var property in definition.PropertyTable) Console.WriteLine($"            {property.Key}: {property.Value}");
        }

        foreach (var definition in definitions.InterfaceDefinitions) Console.WriteLine($"    Interface: {definition.Name}");

        context.AddSource("mooresmaster.g.cs", CodeGenerator.Generate(definitions));
    }

    private ImmutableArray<SchemaFile> ParseAdditionalText(ImmutableArray<AdditionalText> additionalTexts)
    {
        var schemas = new List<SchemaFile>();

        foreach (var additionalText in additionalTexts)
        {
            var text = additionalText.GetText()!.ToString();
            var json = JsonParser.Parse(JsonTokenizer.GetTokens(text));
            var schema = JsonSchemaParser.ParseSchema((json as JsonObject)!);
            schemas.Add(new SchemaFile(additionalText.Path, schema));
        }

        return schemas.ToImmutableArray();
    }
}

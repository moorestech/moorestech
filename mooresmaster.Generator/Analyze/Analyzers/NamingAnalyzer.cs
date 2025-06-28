using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Analyze.Analyzers;

public class NamingAnalyzer : IPostDefinitionLayerAnalyzer
{
    public void PostDefinitionLayerAnalyze(Analysis analysis, Semantics semantics, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable, Definition definition)
    {
        var names = new HashSet<string>();
        return;

        foreach (var typeDefinition in definition.TypeDefinitions)
            if (!names.Add($"{typeDefinition.TypeName.ModuleName}.{typeDefinition.TypeName.Name}"))
                throw new ArgumentException($"Type name '{typeDefinition.TypeName.ModuleName}.{typeDefinition.TypeName.Name}' is not unique");

        foreach (var enumDefinition in definition.InterfaceDefinitions)
            if (!names.Add($"{enumDefinition.TypeName.ModuleName}.{enumDefinition.TypeName.Name}"))
                throw new ArgumentException($"Type name '{enumDefinition.TypeName.ModuleName}.{enumDefinition.TypeName.Name}' is not unique");
    }
}

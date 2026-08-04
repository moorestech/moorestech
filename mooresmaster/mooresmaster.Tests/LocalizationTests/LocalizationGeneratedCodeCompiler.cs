using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

internal static class LocalizationGeneratedCodeCompiler
{
    public static Type CompileTable(string code)
    {
        // Unity互換下限のC#構文として生成ソースを解析する
        // Parse generated source against the minimum Unity-compatible C# syntax
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp7_3);
        var syntaxTree = CSharpSyntaxTree.ParseText(code, parseOptions);
        AssertNoSyntaxErrors(syntaxTree);
        var references = CreatePlatformReferences();
        var compilation = CSharpCompilation.Create(
            $"GeneratedLocalization_{Guid.NewGuid():N}",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // 実assemblyで型と初期化を検証
        // Verify types and initialization in a real assembly
        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        Assert.True(emitResult.Success, FormatDiagnostics(emitResult.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        return assembly.GetType("Mooresmaster.Localization.Generated.VanillaLocalizationTable")!;
    }

    private static List<MetadataReference> CreatePlatformReferences()
    {
        var references = new List<MetadataReference>();
        var platformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (var assemblyPath in platformAssemblies.Split(Path.PathSeparator))
        {
            references.Add(MetadataReference.CreateFromFile(assemblyPath));
        }

        return references;
    }

    private static void AssertNoSyntaxErrors(SyntaxTree syntaxTree)
    {
        foreach (var diagnostic in syntaxTree.GetDiagnostics())
        {
            Assert.True(diagnostic.Severity != DiagnosticSeverity.Error, diagnostic.ToString());
        }
    }

    private static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        var builder = new StringBuilder();
        foreach (var diagnostic in diagnostics)
        {
            builder.AppendLine(diagnostic.ToString());
        }

        return builder.ToString();
    }
}

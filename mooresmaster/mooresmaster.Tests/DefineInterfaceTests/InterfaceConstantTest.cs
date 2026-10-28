using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using mooresmaster.Generator.CodeGenerate;
using Xunit;

namespace mooresmaster.Tests.DefineInterfaceTests;

public class InterfaceConstantTest
{
    private const string Schema = """
        id: constants
        type: object
        defineInterface:
        - interfaceName: IMachine
          properties:
          - key: inputTankCount
            type: integer
          - key: capacity
            type: number
        implementationInterface:
        - IMachine
        interfacePropertyConstants:
          inputTankCount: 0
          capacity: 0
        properties:
        - key: enabled
          type: boolean
        """;

    [Fact]
    public void MissingJsonFieldsBecomeTypedConstantsInCompiledModel()
    {
        var (_, _, semantics, definition, analysis) = Test.Generate(Schema);
        Assert.Empty(analysis.DiagnosticsList);
        var code = string.Join("\n", CodeGenerator.Generate(definition, semantics).Select(file => file.Code));
        Assert.Contains("public int InputTankCount => 0;", code);
        Assert.Contains("public float Capacity => 0f;", code);

        // 実assemblyを生成し、JSONにない値が共通interface経由で0になることを確かめる
        // Compile a real assembly and verify JSON-free values are zero through the shared interface
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = paths.Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var compilation = CSharpCompilation.Create("Constants_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(code) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        var model = assembly.GetTypes().Single(type => type.IsClass);
        var instance = Activator.CreateInstance(model, new object[] { true });
        var contract = model.GetInterfaces().Single();
        Assert.Equal(0, contract.GetProperty("InputTankCount")!.GetValue(instance));
        Assert.Equal(0f, contract.GetProperty("Capacity")!.GetValue(instance));
        Assert.Single(model.GetConstructors().Single().GetParameters());
    }

    [Fact]
    public void ShippedSchemaGeneratesGearTanksOnlyAsConstants()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "VanillaSchema"))) root = root.Parent;
        Assert.NotNull(root);
        var schemas = Directory.GetFiles(Path.Combine(root!.FullName, "VanillaSchema"), "*.yml", SearchOption.AllDirectories).Select(File.ReadAllText).ToArray();
        var (_, _, semantics, definition, _) = Test.Generate(schemas);
        var code = string.Join("\n", CodeGenerator.Generate(definition, semantics).Select(file => file.Code));
        var start = code.IndexOf("public class GearMachineBlockParam", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = code.IndexOf("namespace ", start, StringComparison.Ordinal);
        var gear = end < 0 ? code.Substring(start) : code.Substring(start, end - start);
        Assert.Contains("public int InputTankCount => 0;", gear);
        Assert.Contains("public int OutputTankCount => 0;", gear);
        Assert.Contains("public float InnerTankCapacity => 0f;", gear);
        Assert.DoesNotContain("FluidInventoryConnectors", gear);
    }

    [Fact]
    public void SandboxBlockSchemaKeepsValidYamlIndentation()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "VanillaSchema"))) root = root.Parent;
        Assert.NotNull(root);
        var text = File.ReadAllText(Path.Combine(root!.FullName, "mooresmaster/mooresmaster.SandBox/schema/blocks.yml"));
        var parsed = mooresmaster.Generator.Yaml.YamlParser.Parse("sandbox-blocks.yml", text);
        Assert.NotNull(parsed);
    }

    [Theory]
    [InlineData("inputTankCount: 0", "missing: 0")]
    [InlineData("inputTankCount: 0", "inputTankCount: 0.5")]
    public void InvalidConstantStopsGeneration(string original, string replacement)
    {
        var (_, _, semantics, definition, _) = Test.Generate(Schema.Replace(original, replacement));
        Assert.Throws<InvalidOperationException>(() => CodeGenerator.Generate(definition, semantics));
    }
}

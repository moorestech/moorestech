using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     defineInterfaceのpropertiesがJsonArrayでない場合のDiagnosticsのテスト
/// </summary>
public class InterfacePropertiesNotArrayTest
{
    /// <summary>
    ///     propertiesが配列でない場合のエラーのテスト（ローカル）
    /// </summary>
    [Fact]
    public void InterfacePropertiesNotArrayTest_Local()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName: IMyInterface
                properties: notAnArray
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfacePropertiesNotArrayDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IMyInterface", diagnostics.InterfaceName);
        Assert.False(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     propertiesが配列でない場合のエラーのテスト（グローバル）
    /// </summary>
    [Fact]
    public void InterfacePropertiesNotArrayTest_Global()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            globalDefineInterface:
              - interfaceName: IMyGlobalInterface
                properties: notAnArray
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfacePropertiesNotArrayDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IMyGlobalInterface", diagnostics.InterfaceName);
        Assert.True(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     propertiesが配列の場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void InterfacePropertiesIsArrayTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName: IMyInterface
                properties:
                  - key: prop
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is InterfacePropertiesNotArrayDiagnostics);
    }

    /// <summary>
    ///     propertiesが存在しない場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void InterfacePropertiesMissingTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName: IMyInterface
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is InterfacePropertiesNotArrayDiagnostics);
    }

    /// <summary>
    ///     メッセージが正しいことのテスト
    /// </summary>
    [Fact]
    public void DiagnosticsMessageTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName: IMyInterface
                properties: notAnArray
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfacePropertiesNotArrayDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("properties", diagnostics.Message);
        Assert.Contains("IMyInterface", diagnostics.Message);
        Assert.Contains("array", diagnostics.Message);
    }
}

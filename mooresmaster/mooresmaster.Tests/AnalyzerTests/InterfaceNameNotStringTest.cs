using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     defineInterfaceのinterfaceNameがJsonStringでない場合のDiagnosticsのテスト
/// </summary>
public class InterfaceNameNotStringTest
{
    /// <summary>
    ///     interfaceNameが存在しない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void InterfaceNameMissingTest_Local()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - properties:
                  - key: prop
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfaceNameNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Null(diagnostics.ActualNode);
        Assert.False(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     interfaceNameが文字列でない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void InterfaceNameNotStringTest_Local()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName:
                  - nested
                properties:
                  - key: prop
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfaceNameNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.NotNull(diagnostics.ActualNode);
        Assert.False(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     globalDefineInterfaceでinterfaceNameが存在しない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void InterfaceNameMissingTest_Global()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            globalDefineInterface:
              - properties:
                  - key: prop
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfaceNameNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Null(diagnostics.ActualNode);
        Assert.True(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     正常な場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void InterfaceNameIsStringTest()
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

        Assert.DoesNotContain(diagnosticsArray, d => d is InterfaceNameNotStringDiagnostics);
    }

    /// <summary>
    ///     メッセージが正しいことのテスト（interfaceNameが存在しない場合）
    /// </summary>
    [Fact]
    public void DiagnosticsMessageTest_Missing()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - properties:
                  - key: prop
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfaceNameNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("missing", diagnostics.Message);
        Assert.Contains("interfaceName", diagnostics.Message);
    }

    /// <summary>
    ///     メッセージが正しいことのテスト（interfaceNameが文字列でない場合）
    /// </summary>
    [Fact]
    public void DiagnosticsMessageTest_NotString()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName:
                  - nested
                properties:
                  - key: prop
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfaceNameNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("string", diagnostics.Message);
    }
}

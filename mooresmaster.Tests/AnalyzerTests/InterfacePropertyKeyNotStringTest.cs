using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     defineInterfaceのpropertyのkeyがJsonStringでない場合のDiagnosticsのテスト
/// </summary>
public class InterfacePropertyKeyNotStringTest
{
    /// <summary>
    ///     keyが存在しない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void InterfacePropertyKeyMissingTest()
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
                  - type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfacePropertyKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IMyInterface", diagnostics.InterfaceName);
        Assert.Equal(0, diagnostics.PropertyIndex);
        Assert.Null(diagnostics.ActualKeyNode);
        Assert.False(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     keyが文字列でない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void InterfacePropertyKeyNotStringTest_NotString()
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
                  - key:
                      - nested
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfacePropertyKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IMyInterface", diagnostics.InterfaceName);
        Assert.Equal(0, diagnostics.PropertyIndex);
        Assert.NotNull(diagnostics.ActualKeyNode);
        Assert.False(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     2番目のプロパティでkeyがない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void InterfacePropertyKeyMissingTest_SecondProperty()
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
                  - key: prop1
                    type: string
                  - type: integer
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfacePropertyKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IMyInterface", diagnostics.InterfaceName);
        Assert.Equal(1, diagnostics.PropertyIndex);
    }

    /// <summary>
    ///     正常な場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void InterfacePropertyKeyIsStringTest()
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

        Assert.DoesNotContain(diagnosticsArray, d => d is InterfacePropertyKeyNotStringDiagnostics);
    }

    /// <summary>
    ///     メッセージが正しいことのテスト（keyが存在しない場合）
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
              - interfaceName: IMyInterface
                properties:
                  - type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfacePropertyKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("missing", diagnostics.Message);
        Assert.Contains("key", diagnostics.Message);
        Assert.Contains("index 0", diagnostics.Message);
        Assert.Contains("IMyInterface", diagnostics.Message);
    }

    /// <summary>
    ///     メッセージが正しいことのテスト（keyが文字列でない場合）
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
              - interfaceName: IMyInterface
                properties:
                  - key:
                      - nested
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InterfacePropertyKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("string", diagnostics.Message);
    }
}

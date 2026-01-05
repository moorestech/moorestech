using System.Linq;
using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     defineInterfaceのimplementationInterface配列の要素がJsonStringでない場合のDiagnosticsのテスト
/// </summary>
public class ImplementsElementNotStringTest
{
    /// <summary>
    ///     implementationInterface配列の要素が数値の場合のエラーのテスト（ローカル）
    /// </summary>
    [Fact]
    public void ImplementsElementNotStringTest_Number_Local()
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
                implementationInterface:
                  - 123
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ImplementsElementNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IMyInterface", diagnostics.InterfaceName);
        Assert.Equal(0, diagnostics.Index);
        Assert.False(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     implementationInterface配列の2番目の要素が数値の場合のエラーのテスト
    /// </summary>
    [Fact]
    public void ImplementsElementNotStringTest_SecondElement()
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
                implementationInterface:
                  - IValidInterface
                  - 456
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        var implementsDiagnostics = diagnosticsArray.OfType<ImplementsElementNotStringDiagnostics>().ToArray();
        Assert.Single(implementsDiagnostics);
        var diagnostics = implementsDiagnostics[0];
        Assert.Equal("IMyInterface", diagnostics.InterfaceName);
        Assert.Equal(1, diagnostics.Index);
    }

    /// <summary>
    ///     globalDefineInterfaceでimplementationInterface配列の要素が数値の場合のエラーのテスト
    /// </summary>
    [Fact]
    public void ImplementsElementNotStringTest_Global()
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
                properties:
                  - key: prop
                    type: string
                implementationInterface:
                  - 789
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ImplementsElementNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IMyGlobalInterface", diagnostics.InterfaceName);
        Assert.True(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     複数の要素が数値の場合のエラーのテスト
    /// </summary>
    [Fact]
    public void ImplementsElementNotStringTest_MultipleElements()
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
                implementationInterface:
                  - 111
                  - 222
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Equal(2, diagnosticsArray.Count);
        var diag1 = Assert.IsType<ImplementsElementNotStringDiagnostics>(diagnosticsArray[0]);
        var diag2 = Assert.IsType<ImplementsElementNotStringDiagnostics>(diagnosticsArray[1]);
        Assert.Equal(0, diag1.Index);
        Assert.Equal(1, diag2.Index);
    }

    /// <summary>
    ///     正常な場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void ImplementsElementIsStringTest()
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
                implementationInterface:
                  - IValidInterface1
                  - IValidInterface2
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is ImplementsElementNotStringDiagnostics);
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
                properties:
                  - key: prop
                    type: string
                implementationInterface:
                  - 999
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ImplementsElementNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("index 0", diagnostics.Message);
        Assert.Contains("implements", diagnostics.Message);
        Assert.Contains("IMyInterface", diagnostics.Message);
        Assert.Contains("string", diagnostics.Message);
    }
}

using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     defineInterfaceまたはglobalDefineInterfaceの配列要素がオブジェクトでない場合のDiagnosticsのテスト
/// </summary>
public class DefineInterfaceElementNotObjectTest
{
    /// <summary>
    ///     defineInterfaceの配列要素がオブジェクトでない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void DefineInterfaceElementNotObjectTest_Local()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - notAnObject
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DefineInterfaceElementNotObjectDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("defineInterface", diagnostics.FieldName);
        Assert.Equal(0, diagnostics.Index);
        Assert.False(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     globalDefineInterfaceの配列要素がオブジェクトでない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void DefineInterfaceElementNotObjectTest_Global()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            globalDefineInterface:
              - notAnObject
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DefineInterfaceElementNotObjectDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("globalDefineInterface", diagnostics.FieldName);
        Assert.Equal(0, diagnostics.Index);
        Assert.True(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     配列の2番目の要素がオブジェクトでない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void DefineInterfaceElementNotObjectTest_SecondElement()
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
              - notAnObject
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DefineInterfaceElementNotObjectDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("defineInterface", diagnostics.FieldName);
        Assert.Equal(1, diagnostics.Index);
    }

    /// <summary>
    ///     複数の要素がオブジェクトでない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void DefineInterfaceElementNotObjectTest_MultipleElements()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - notAnObject1
              - notAnObject2
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Equal(2, diagnosticsArray.Count);
        var diag1 = Assert.IsType<DefineInterfaceElementNotObjectDiagnostics>(diagnosticsArray[0]);
        var diag2 = Assert.IsType<DefineInterfaceElementNotObjectDiagnostics>(diagnosticsArray[1]);
        Assert.Equal(0, diag1.Index);
        Assert.Equal(1, diag2.Index);
    }

    /// <summary>
    ///     配列要素がオブジェクトの場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void DefineInterfaceElementIsObjectTest()
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
                  - key: interfaceProp
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is DefineInterfaceElementNotObjectDiagnostics);
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
              - notAnObject
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DefineInterfaceElementNotObjectDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("index 0", diagnostics.Message);
        Assert.Contains("defineInterface", diagnostics.Message);
        Assert.Contains("object", diagnostics.Message);
    }
}

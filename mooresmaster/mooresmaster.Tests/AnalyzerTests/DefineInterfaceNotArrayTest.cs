using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     defineInterfaceまたはglobalDefineInterfaceの値が配列でない場合のDiagnosticsのテスト
/// </summary>
public class DefineInterfaceNotArrayTest
{
    /// <summary>
    ///     defineInterfaceが配列でない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void DefineInterfaceNotArrayTest_Local()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface: notAnArray
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DefineInterfaceNotArrayDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("defineInterface", diagnostics.FieldName);
        Assert.False(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     globalDefineInterfaceが配列でない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void DefineInterfaceNotArrayTest_Global()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            globalDefineInterface: notAnArray
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DefineInterfaceNotArrayDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("globalDefineInterface", diagnostics.FieldName);
        Assert.True(diagnostics.IsGlobal);
    }

    /// <summary>
    ///     defineInterfaceが配列の場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void DefineInterfaceArrayTest_Local()
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

        Assert.DoesNotContain(diagnosticsArray, d => d is DefineInterfaceNotArrayDiagnostics);
    }

    /// <summary>
    ///     globalDefineInterfaceが配列の場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void DefineInterfaceArrayTest_Global()
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
                  - key: interfaceProp
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is DefineInterfaceNotArrayDiagnostics);
    }

    /// <summary>
    ///     Locationが正しくセットされていることのテスト
    /// </summary>
    [Fact]
    public void DiagnosticsContainsCorrectLocationTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface: notAnArray
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DefineInterfaceNotArrayDiagnostics>(diagnosticsArray[0]);
        Assert.Single(diagnostics.Locations);
        // LocationがdefineInterfaceの値の位置を指していること（6行目）
        Assert.Equal(6, diagnostics.Locations[0].StartLine);
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
            defineInterface: notAnArray
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DefineInterfaceNotArrayDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("defineInterface", diagnostics.Message);
        Assert.Contains("array", diagnostics.Message);
    }
}

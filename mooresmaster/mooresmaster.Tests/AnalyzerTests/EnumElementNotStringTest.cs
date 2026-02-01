using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     enum/optionsの配列要素がJsonStringでない場合のDiagnosticsのテスト
/// </summary>
public class EnumElementNotStringTest
{
    /// <summary>
    ///     string型のenum配列要素が文字列でない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void EnumElementNotStringTest_StringType()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
                enum:
                  - 123
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<EnumElementNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myProperty", diagnostics.PropertyName);
        Assert.Equal(0, diagnostics.Index);
        Assert.False(diagnostics.IsEnumType);
    }

    /// <summary>
    ///     enum型のoptions配列要素が文字列でない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void EnumElementNotStringTest_EnumType()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: enum
                options:
                  - 456
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<EnumElementNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myProperty", diagnostics.PropertyName);
        Assert.Equal(0, diagnostics.Index);
        Assert.True(diagnostics.IsEnumType);
    }

    /// <summary>
    ///     複数の要素が文字列でない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void EnumElementNotStringTest_MultipleElements()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
                enum:
                  - 111
                  - 222
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Equal(2, diagnosticsArray.Count);
        var diag1 = Assert.IsType<EnumElementNotStringDiagnostics>(diagnosticsArray[0]);
        var diag2 = Assert.IsType<EnumElementNotStringDiagnostics>(diagnosticsArray[1]);
        Assert.Equal(0, diag1.Index);
        Assert.Equal(1, diag2.Index);
    }

    /// <summary>
    ///     正常な場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void EnumElementIsStringTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
                enum:
                  - value1
                  - value2
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is EnumElementNotStringDiagnostics);
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
                enum:
                  - 123
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<EnumElementNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("index 0", diagnostics.Message);
        Assert.Contains("enum", diagnostics.Message);
        Assert.Contains("myProperty", diagnostics.Message);
        Assert.Contains("string", diagnostics.Message);
    }
}

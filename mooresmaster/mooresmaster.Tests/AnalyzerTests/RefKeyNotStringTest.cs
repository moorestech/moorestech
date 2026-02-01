using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     refの値がJsonStringでない場合のDiagnosticsのテスト
/// </summary>
public class RefKeyNotStringTest
{
    /// <summary>
    ///     refの値が配列の場合のエラーのテスト
    /// </summary>
    [Fact]
    public void RefKeyNotStringTest_Array()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                ref:
                  - notAString
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RefKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myProperty", diagnostics.PropertyName);
    }

    /// <summary>
    ///     refの値が数値の場合のエラーのテスト
    /// </summary>
    [Fact]
    public void RefKeyNotStringTest_Number()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                ref: 123
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RefKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myProperty", diagnostics.PropertyName);
    }

    /// <summary>
    ///     refが正常な場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void RefKeyIsStringTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                ref: otherSchema
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is RefKeyNotStringDiagnostics);
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
                ref: 123
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RefKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("ref", diagnostics.Message);
        Assert.Contains("myProperty", diagnostics.Message);
        Assert.Contains("string", diagnostics.Message);
    }
}

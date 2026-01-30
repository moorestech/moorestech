using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     ルートスキーマにidが定義されていない場合のDiagnosticsのテスト
/// </summary>
public class RootIdNotFoundTest
{
    /// <summary>
    ///     ルートにidがない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void RootWithoutIdTest()
    {
        const string schema =
            """
            type: object
            properties:
              - key: myProperty
                type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RootIdNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.NotNull(diagnostics.RootJson);
    }

    /// <summary>
    ///     ルートにidがある場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void RootWithIdTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Empty(diagnosticsArray);
    }

    /// <summary>
    ///     ネストされたオブジェクトでルートにidがない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void NestedObjectWithoutIdTest()
    {
        const string schema =
            """
            type: object
            properties:
              - key: nested
                type: object
                properties:
                  - key: innerProperty
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RootIdNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.NotNull(diagnostics.RootJson);
    }

    /// <summary>
    ///     Locationが正しくセットされていることのテスト
    /// </summary>
    [Fact]
    public void DiagnosticsContainsCorrectLocationTest()
    {
        const string schema =
            """
            type: object
            properties:
              - key: myProperty
                type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RootIdNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Single(diagnostics.Locations);
        // Locationがルートオブジェクトの位置を指していること
        Assert.Equal(1, diagnostics.Locations[0].StartLine);
    }

    /// <summary>
    ///     メッセージが正しいことのテスト
    /// </summary>
    [Fact]
    public void DiagnosticsMessageTest()
    {
        const string schema =
            """
            type: object
            properties:
              - key: myProperty
                type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RootIdNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("Root schema must have an 'id' field.", diagnostics.Message);
    }
}

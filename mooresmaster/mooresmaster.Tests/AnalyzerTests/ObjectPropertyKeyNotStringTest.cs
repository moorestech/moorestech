using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     objectのpropertiesの要素のkeyがJsonStringでない場合のDiagnosticsのテスト
/// </summary>
public class ObjectPropertyKeyNotStringTest
{
    /// <summary>
    ///     keyが存在しない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void ObjectPropertyKeyMissingTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ObjectPropertyKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Null(diagnostics.ParentObjectName);
        Assert.Equal(0, diagnostics.PropertyIndex);
        Assert.Null(diagnostics.ActualKeyNode);
    }

    /// <summary>
    ///     keyが数値の場合のエラーのテスト
    /// </summary>
    [Fact]
    public void ObjectPropertyKeyNotStringTest_Number()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: 123
                type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ObjectPropertyKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Null(diagnostics.ParentObjectName);
        Assert.Equal(0, diagnostics.PropertyIndex);
        Assert.NotNull(diagnostics.ActualKeyNode);
    }

    /// <summary>
    ///     ネストしたobjectのpropertiesでkeyが不正な場合のテスト
    /// </summary>
    [Fact]
    public void ObjectPropertyKeyNotStringTest_Nested()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: nestedObject
                type: object
                properties:
                  - key: 456
                    type: number
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ObjectPropertyKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("nestedObject", diagnostics.ParentObjectName);
        Assert.Equal(0, diagnostics.PropertyIndex);
        Assert.NotNull(diagnostics.ActualKeyNode);
    }

    /// <summary>
    ///     複数のエラーがある場合のテスト
    /// </summary>
    [Fact]
    public void ObjectPropertyKeyNotStringTest_Multiple()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: 111
                type: string
              - key: validKey
                type: number
              - key: 222
                type: boolean
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Equal(2, diagnosticsArray.Count);
        var diag1 = Assert.IsType<ObjectPropertyKeyNotStringDiagnostics>(diagnosticsArray[0]);
        var diag2 = Assert.IsType<ObjectPropertyKeyNotStringDiagnostics>(diagnosticsArray[1]);
        Assert.Equal(0, diag1.PropertyIndex);
        Assert.Equal(2, diag2.PropertyIndex);
    }

    /// <summary>
    ///     正常な場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void ObjectPropertyKeyIsStringTest()
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

        Assert.DoesNotContain(diagnosticsArray, d => d is ObjectPropertyKeyNotStringDiagnostics);
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
              - type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ObjectPropertyKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("index 0", diagnostics.Message);
        Assert.Contains("missing", diagnostics.Message);
        Assert.Contains("key", diagnostics.Message);
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
              - key: nestedObject
                type: object
                properties:
                  - key: 123
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ObjectPropertyKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("index 0", diagnostics.Message);
        Assert.Contains("nestedObject", diagnostics.Message);
        Assert.Contains("string", diagnostics.Message);
    }
}

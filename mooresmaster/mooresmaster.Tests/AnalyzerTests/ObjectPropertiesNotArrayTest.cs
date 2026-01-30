using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     objectのpropertiesがJsonArrayでない場合のDiagnosticsのテスト
/// </summary>
public class ObjectPropertiesNotArrayTest
{
    /// <summary>
    ///     propertiesが配列でない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void ObjectPropertiesNotArrayTest_NotArray()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties: notAnArray
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ObjectPropertiesNotArrayDiagnostics>(diagnosticsArray[0]);
        Assert.Null(diagnostics.PropertyName);
    }

    /// <summary>
    ///     ネストしたobjectのpropertiesが配列でない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void ObjectPropertiesNotArrayTest_Nested()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: nestedObject
                type: object
                properties: notAnArray
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ObjectPropertiesNotArrayDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("nestedObject", diagnostics.PropertyName);
    }

    /// <summary>
    ///     propertiesが配列の場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void ObjectPropertiesIsArrayTest()
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

        Assert.DoesNotContain(diagnosticsArray, d => d is ObjectPropertiesNotArrayDiagnostics);
    }

    /// <summary>
    ///     propertiesが存在しない場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void ObjectPropertiesMissingTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is ObjectPropertiesNotArrayDiagnostics);
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
              - key: nestedObject
                type: object
                properties: notAnArray
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ObjectPropertiesNotArrayDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("properties", diagnostics.Message);
        Assert.Contains("nestedObject", diagnostics.Message);
        Assert.Contains("array", diagnostics.Message);
    }
}

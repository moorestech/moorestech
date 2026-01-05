using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     スキーマのtypeが不明な値の場合のDiagnosticsのテスト
/// </summary>
public class UnknownTypeTest
{
    /// <summary>
    ///     不明なtypeの場合のエラーのテスト
    /// </summary>
    [Fact]
    public void PropertyWithUnknownTypeTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: unknownType
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<UnknownTypeDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myProperty", diagnostics.PropertyName);
        Assert.Equal("unknownType", diagnostics.UnknownType);
    }

    /// <summary>
    ///     有効なtypeの場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void PropertyWithValidTypeTest()
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
    ///     ネストされたオブジェクト内で不明なtypeの場合のエラーのテスト
    /// </summary>
    [Fact]
    public void NestedObjectWithUnknownTypeTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: nested
                type: object
                properties:
                  - key: innerProperty
                    type: invalidType
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<UnknownTypeDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("innerProperty", diagnostics.PropertyName);
        Assert.Equal("invalidType", diagnostics.UnknownType);
    }

    /// <summary>
    ///     配列のitemsで不明なtypeの場合のエラーのテスト
    /// </summary>
    [Fact]
    public void ArrayItemsWithUnknownTypeTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myArray
                type: array
                items:
                  type: badType
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<UnknownTypeDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("badType", diagnostics.UnknownType);
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
                type: unknownType
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<UnknownTypeDiagnostics>(diagnosticsArray[0]);
        Assert.Single(diagnostics.Locations);
        // Locationがtypeの値の位置を指していること（5行目）
        Assert.Equal(5, diagnostics.Locations[0].StartLine);
    }

    /// <summary>
    ///     メッセージが正しいことのテスト（プロパティ名あり）
    /// </summary>
    [Fact]
    public void DiagnosticsMessageWithPropertyNameTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: unknownType
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<UnknownTypeDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("myProperty", diagnostics.Message);
        Assert.Contains("unknownType", diagnostics.Message);
    }

    /// <summary>
    ///     複数のプロパティで不明なtypeの場合、複数のエラーが出るテスト
    /// </summary>
    [Fact]
    public void MultiplePropertiesWithUnknownTypeTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: property1
                type: badType1
              - key: property2
                type: badType2
              - key: property3
                type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Equal(2, diagnosticsArray.Count);
        var diag1 = Assert.IsType<UnknownTypeDiagnostics>(diagnosticsArray[0]);
        var diag2 = Assert.IsType<UnknownTypeDiagnostics>(diagnosticsArray[1]);
        Assert.Equal("property1", diag1.PropertyName);
        Assert.Equal("badType1", diag1.UnknownType);
        Assert.Equal("property2", diag2.PropertyName);
        Assert.Equal("badType2", diag2.UnknownType);
    }

    /// <summary>
    ///     全ての有効なtypeでエラーが出ないテスト
    /// </summary>
    [Theory]
    [InlineData("string")]
    [InlineData("integer")]
    [InlineData("number")]
    [InlineData("boolean")]
    [InlineData("uuid")]
    [InlineData("vector2")]
    [InlineData("vector3")]
    [InlineData("vector4")]
    [InlineData("vector2Int")]
    [InlineData("vector3Int")]
    public void AllValidTypesTest(string validType)
    {
        var schema = $"""
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: {validType}
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is UnknownTypeDiagnostics);
    }
}

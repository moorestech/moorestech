using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     スキーマにtypeが定義されていない場合のDiagnosticsのテスト
/// </summary>
public class TypeNotFoundTest
{
    /// <summary>
    ///     typeがない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void PropertyWithoutTypeTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<TypeNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myProperty", diagnostics.PropertyName);
    }

    /// <summary>
    ///     typeがある場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void PropertyWithTypeTest()
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
    ///     refがある場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void PropertyWithRefTest()
    {
        const string schema1 =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                ref: otherSchema
            """;

        const string schema2 =
            """
            id: otherSchema
            type: object
            properties:
              - key: otherProperty
                type: string
            """;

        var diagnosticsArray = Test.Generate(schema1, schema2).analysis.DiagnosticsList;

        Assert.Empty(diagnosticsArray);
    }

    /// <summary>
    ///     switchがある場合はTypeNotFoundエラーが出ないテスト
    /// </summary>
    [Fact]
    public void PropertyWithSwitchTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: kind
                type: string
                enum:
                  - A
                  - B
              - key: data
                switch: ./kind
                case:
                  - when: A
                    type: object
                    properties:
                      - key: aValue
                        type: integer
                  - when: B
                    type: object
                    properties:
                      - key: bValue
                        type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        // TypeNotFoundDiagnosticsが出ていないことを確認
        Assert.DoesNotContain(diagnosticsArray, d => d is TypeNotFoundDiagnostics);
    }

    /// <summary>
    ///     ネストされたオブジェクト内でtypeがない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void NestedObjectWithoutTypeTest()
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
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<TypeNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("innerProperty", diagnostics.PropertyName);
    }

    /// <summary>
    ///     配列のitemsにtypeがない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void ArrayItemsWithoutTypeTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myArray
                type: array
                items:
                  propertyName: myArrayElement
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<TypeNotFoundDiagnostics>(diagnosticsArray[0]);
        // 配列のitemsではpropertyNameがkeyとして取得されないためnullになる
        Assert.Null(diagnostics.PropertyName);
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
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<TypeNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Single(diagnostics.Locations);
        // Locationがプロパティの位置を指していること（4行目）
        Assert.Equal(4, diagnostics.Locations[0].StartLine);
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
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<TypeNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("Property 'myProperty' must have a 'type', 'ref', or 'switch' field.", diagnostics.Message);
    }

    /// <summary>
    ///     複数のプロパティにtypeがない場合、複数のエラーが出るテスト
    /// </summary>
    [Fact]
    public void MultiplePropertiesWithoutTypeTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: property1
              - key: property2
              - key: property3
                type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Equal(2, diagnosticsArray.Count);
        var diag1 = Assert.IsType<TypeNotFoundDiagnostics>(diagnosticsArray[0]);
        var diag2 = Assert.IsType<TypeNotFoundDiagnostics>(diagnosticsArray[1]);
        Assert.Equal("property1", diag1.PropertyName);
        Assert.Equal("property2", diag2.PropertyName);
    }
}

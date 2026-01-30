using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

public class InvalidOptionalValueTest
{
    /// <summary>
    ///     optionalがtrueの場合はエラーが出ないテスト（boolean）
    /// </summary>
    [Fact]
    public void OptionalTrueBooleanTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: name
                type: string
                optional: true
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Empty(diagnosticsArray);
    }
    
    /// <summary>
    ///     optionalがfalseの場合はエラーが出ないテスト（boolean）
    /// </summary>
    [Fact]
    public void OptionalFalseBooleanTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: name
                type: string
                optional: false
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Empty(diagnosticsArray);
    }
    
    /// <summary>
    ///     optionalが"true"の場合はエラーが出ないテスト（string）
    /// </summary>
    [Fact]
    public void OptionalTrueStringTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: name
                type: string
                optional: "true"
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Empty(diagnosticsArray);
    }
    
    /// <summary>
    ///     optionalが"false"の場合はエラーが出ないテスト（string）
    /// </summary>
    [Fact]
    public void OptionalFalseStringTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: name
                type: string
                optional: "false"
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Empty(diagnosticsArray);
    }
    
    /// <summary>
    ///     optionalが定義されていない場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void OptionalNotDefinedTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: name
                type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Empty(diagnosticsArray);
    }
    
    /// <summary>
    ///     optionalが無効な文字列の場合のエラーのテスト
    /// </summary>
    [Fact]
    public void InvalidOptionalStringValueTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: name
                type: string
                optional: "yes"
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InvalidOptionalValueDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("name", diagnostics.PropertyName);
        Assert.Single(diagnostics.Locations);
    }
    
    /// <summary>
    ///     optionalが数値の場合のエラーのテスト
    /// </summary>
    [Fact]
    public void InvalidOptionalNumberValueTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: count
                type: integer
                optional: 1
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InvalidOptionalValueDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("count", diagnostics.PropertyName);
        Assert.Single(diagnostics.Locations);
    }
    
    /// <summary>
    ///     optionalが0の場合のエラーのテスト
    /// </summary>
    [Fact]
    public void InvalidOptionalZeroValueTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: value
                type: number
                optional: 0
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InvalidOptionalValueDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("value", diagnostics.PropertyName);
    }
    
    /// <summary>
    ///     複数のプロパティで無効なoptionalがある場合のエラーのテスト
    /// </summary>
    [Fact]
    public void MultipleInvalidOptionalValuesTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: name
                type: string
                optional: "yes"
              - key: count
                type: integer
                optional: 1
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Equal(2, diagnosticsArray.Count);
        
        var diagnostics1 = Assert.IsType<InvalidOptionalValueDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("name", diagnostics1.PropertyName);
        
        var diagnostics2 = Assert.IsType<InvalidOptionalValueDiagnostics>(diagnosticsArray[1]);
        Assert.Equal("count", diagnostics2.PropertyName);
    }
    
    /// <summary>
    ///     ネストされたオブジェクトでの無効なoptionalのテスト
    /// </summary>
    [Fact]
    public void NestedObjectInvalidOptionalTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: outer
                type: object
                properties:
                  - key: inner
                    type: string
                    optional: "maybe"
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InvalidOptionalValueDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("inner", diagnostics.PropertyName);
    }
    
    /// <summary>
    ///     配列内の要素での無効なoptionalのテスト
    /// </summary>
    [Fact]
    public void ArrayItemInvalidOptionalTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: items
                type: array
                items:
                  type: object
                  properties:
                    - key: value
                      type: string
                      optional: "nope"
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InvalidOptionalValueDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("value", diagnostics.PropertyName);
    }
    
    /// <summary>
    ///     switchのcaseでの無効なoptionalのテスト
    /// </summary>
    [Fact]
    public void SwitchCaseInvalidOptionalTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: type
                type: enum
                options:
                  - A
                  - B
              - key: data
                switch: ./type
                cases:
                  - when: A
                    type: object
                    properties:
                      - key: valueA
                        type: string
                        optional: "invalid"
                  - when: B
                    type: object
                    properties:
                      - key: valueB
                        type: number
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InvalidOptionalValueDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("valueA", diagnostics.PropertyName);
    }
    
    /// <summary>
    ///     エラーメッセージに正しい値が含まれていることのテスト
    /// </summary>
    [Fact]
    public void DiagnosticsMessageContainsActualValueTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: name
                type: string
                optional: "invalid_value"
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InvalidOptionalValueDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("invalid_value", diagnostics.Message);
        Assert.Contains("name", diagnostics.Message);
    }
}
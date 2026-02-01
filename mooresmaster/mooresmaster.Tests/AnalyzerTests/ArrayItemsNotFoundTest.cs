using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

public class ArrayItemsNotFoundTest
{
    /// <summary>
    ///     配列にitemsが定義されていない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void ArrayWithoutItemsTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myArray
                type: array
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ArrayItemsNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myArray", diagnostics.PropertyName);
    }
    
    /// <summary>
    ///     配列にitemsが定義されている場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void ArrayWithItemsTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myArray
                type: array
                items:
                  type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Empty(diagnosticsArray);
    }
    
    /// <summary>
    ///     複数の配列でitemsが定義されていない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void MultipleArraysWithoutItemsTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: array1
                type: array
              - key: array2
                type: array
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Equal(2, diagnosticsArray.Count);
        
        var diagnostics1 = Assert.IsType<ArrayItemsNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("array1", diagnostics1.PropertyName);
        
        var diagnostics2 = Assert.IsType<ArrayItemsNotFoundDiagnostics>(diagnosticsArray[1]);
        Assert.Equal("array2", diagnostics2.PropertyName);
    }
    
    /// <summary>
    ///     ネストされた配列でitemsが定義されていない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void NestedArrayWithoutItemsTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: outerArray
                type: array
                items:
                  type: object
                  properties:
                    - key: innerArray
                      type: array
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ArrayItemsNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("innerArray", diagnostics.PropertyName);
    }
    
    /// <summary>
    ///     ルートが配列型でitemsが定義されていない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void RootArrayWithoutItemsTest()
    {
        const string schema =
            """
            id: testSchema
            type: array
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ArrayItemsNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Null(diagnostics.PropertyName);
    }
    
    /// <summary>
    ///     Diagnosticsに正しいJsonObjectとSchemaIdが含まれていることのテスト
    /// </summary>
    [Fact]
    public void DiagnosticsContainsCorrectDataTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myArray
                type: array
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<ArrayItemsNotFoundDiagnostics>(diagnosticsArray[0]);
        
        Assert.NotNull(diagnostics.ArrayJson);
        Assert.Single(diagnostics.Locations);
    }
}
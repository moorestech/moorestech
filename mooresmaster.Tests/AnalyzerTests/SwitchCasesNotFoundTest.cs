using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

public class SwitchCasesNotFoundTest
{
    /// <summary>
    ///     switchにcasesが定義されていない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void SwitchWithoutCasesTest()
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
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<SwitchCasesNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("data", diagnostics.PropertyName);
        Assert.Equal("./type", diagnostics.SwitchPath);
    }
    
    /// <summary>
    ///     switchにcasesが定義されている場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void SwitchWithCasesTest()
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
                  - when: B
                    type: object
                    properties:
                      - key: valueB
                        type: number
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Empty(diagnosticsArray);
    }
    
    /// <summary>
    ///     複数のswitchでcasesが定義されていない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void MultipleSwitchesWithoutCasesTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: type1
                type: enum
                options:
                  - A
                  - B
              - key: type2
                type: enum
                options:
                  - X
                  - Y
              - key: data1
                switch: ./type1
              - key: data2
                switch: ./type2
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Equal(2, diagnosticsArray.Count);
        
        var diagnostics1 = Assert.IsType<SwitchCasesNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("data1", diagnostics1.PropertyName);
        
        var diagnostics2 = Assert.IsType<SwitchCasesNotFoundDiagnostics>(diagnosticsArray[1]);
        Assert.Equal("data2", diagnostics2.PropertyName);
    }
    
    /// <summary>
    ///     ネストされたswitchでcasesが定義されていない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void NestedSwitchWithoutCasesTest()
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
                      - key: innerType
                        type: enum
                        options:
                          - X
                          - Y
                      - key: innerData
                        switch: ./innerType
                  - when: B
                    type: object
                    properties:
                      - key: value
                        type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<SwitchCasesNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("innerData", diagnostics.PropertyName);
        Assert.Equal("./innerType", diagnostics.SwitchPath);
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
              - key: type
                type: enum
                options:
                  - A
                  - B
              - key: data
                switch: ./type
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<SwitchCasesNotFoundDiagnostics>(diagnosticsArray[0]);
        
        Assert.NotNull(diagnostics.SwitchJson);
        Assert.Single(diagnostics.Locations);
    }
}
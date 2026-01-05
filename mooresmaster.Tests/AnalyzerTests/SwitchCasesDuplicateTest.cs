using System.Linq;
using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

public class SwitchCasesDuplicateTest
{
    /// <summary>
    ///     重複がない場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void NoDuplicateCasesTest()
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
                  - C
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
                        type: string
                  - when: C
                    type: object
                    properties:
                      - key: valueC
                        type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.DoesNotContain(diagnosticsArray, d => d is SwitchCasesDuplicateDiagnostics);
    }
    
    /// <summary>
    ///     1つのケースが重複している場合のエラーのテスト
    /// </summary>
    [Fact]
    public void SingleDuplicateCaseTest()
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
                      - key: valueA1
                        type: string
                  - when: A
                    type: object
                    properties:
                      - key: valueA2
                        type: string
                  - when: B
                    type: object
                    properties:
                      - key: valueB
                        type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var duplicateDiagnostics = diagnosticsArray.OfType<SwitchCasesDuplicateDiagnostics>().ToArray();
        
        Assert.Single(duplicateDiagnostics);
        Assert.Equal("A", duplicateDiagnostics[0].DuplicateCase);
        Assert.Equal(2, duplicateDiagnostics[0].OccurrenceCount);
    }
    
    /// <summary>
    ///     同じケースが3回以上重複している場合のテスト
    /// </summary>
    [Fact]
    public void TripleDuplicateCaseTest()
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
                      - key: value1
                        type: string
                  - when: A
                    type: object
                    properties:
                      - key: value2
                        type: string
                  - when: A
                    type: object
                    properties:
                      - key: value3
                        type: string
                  - when: B
                    type: object
                    properties:
                      - key: valueB
                        type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var duplicateDiagnostics = diagnosticsArray.OfType<SwitchCasesDuplicateDiagnostics>().ToArray();
        
        Assert.Single(duplicateDiagnostics);
        Assert.Equal("A", duplicateDiagnostics[0].DuplicateCase);
        Assert.Equal(3, duplicateDiagnostics[0].OccurrenceCount);
    }
    
    /// <summary>
    ///     複数のケースが重複している場合のテスト
    /// </summary>
    [Fact]
    public void MultipleDuplicateCasesTest()
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
                  - C
              - key: data
                switch: ./type
                cases:
                  - when: A
                    type: object
                    properties:
                      - key: value1
                        type: string
                  - when: A
                    type: object
                    properties:
                      - key: value2
                        type: string
                  - when: B
                    type: object
                    properties:
                      - key: value3
                        type: string
                  - when: B
                    type: object
                    properties:
                      - key: value4
                        type: string
                  - when: C
                    type: object
                    properties:
                      - key: valueC
                        type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var duplicateDiagnostics = diagnosticsArray.OfType<SwitchCasesDuplicateDiagnostics>().ToArray();
        
        Assert.Equal(2, duplicateDiagnostics.Length);
        
        var diagA = duplicateDiagnostics.First(d => d.DuplicateCase == "A");
        Assert.Equal(2, diagA.OccurrenceCount);
        
        var diagB = duplicateDiagnostics.First(d => d.DuplicateCase == "B");
        Assert.Equal(2, diagB.OccurrenceCount);
    }
    
    /// <summary>
    ///     ネストされたswitchでの重複チェックのテスト
    /// </summary>
    [Fact]
    public void NestedSwitchDuplicateCaseTest()
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
                        cases:
                          - when: X
                            type: object
                            properties:
                              - key: value1
                                type: string
                          - when: X
                            type: object
                            properties:
                              - key: value2
                                type: string
                          - when: Y
                            type: object
                            properties:
                              - key: valueY
                                type: string
                  - when: B
                    type: object
                    properties:
                      - key: value
                        type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var duplicateDiagnostics = diagnosticsArray.OfType<SwitchCasesDuplicateDiagnostics>().ToArray();
        
        Assert.Single(duplicateDiagnostics);
        Assert.Equal("X", duplicateDiagnostics[0].DuplicateCase);
        Assert.Equal(2, duplicateDiagnostics[0].OccurrenceCount);
    }
    
    /// <summary>
    ///     Diagnosticsに正しいLocation情報が含まれていることのテスト
    /// </summary>
    [Fact]
    public void DiagnosticsContainsLocationTest()
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
                      - key: value1
                        type: string
                  - when: A
                    type: object
                    properties:
                      - key: value2
                        type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var diagnostics = diagnosticsArray.OfType<SwitchCasesDuplicateDiagnostics>().First();
        
        Assert.Single(diagnostics.Locations);
        Assert.True(diagnostics.Locations[0].StartLine > 0);
    }
    
    /// <summary>
    ///     Diagnosticsのメッセージに必要な情報が含まれていることのテスト
    /// </summary>
    [Fact]
    public void DiagnosticsMessageTest()
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
                      - key: value1
                        type: string
                  - when: A
                    type: object
                    properties:
                      - key: value2
                        type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var diagnostics = diagnosticsArray.OfType<SwitchCasesDuplicateDiagnostics>().First();
        
        Assert.Contains("A", diagnostics.Message);
        Assert.Contains("2", diagnostics.Message);
        Assert.Contains("Duplicate", diagnostics.Message);
    }
    
    /// <summary>
    ///     重複と網羅性エラーが同時に発生する場合のテスト
    /// </summary>
    [Fact]
    public void DuplicateAndNotExhaustiveTest()
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
                  - C
              - key: data
                switch: ./type
                cases:
                  - when: A
                    type: object
                    properties:
                      - key: value1
                        type: string
                  - when: A
                    type: object
                    properties:
                      - key: value2
                        type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        // 重複エラーがある
        var duplicateDiagnostics = diagnosticsArray.OfType<SwitchCasesDuplicateDiagnostics>().ToArray();
        Assert.Single(duplicateDiagnostics);
        Assert.Equal("A", duplicateDiagnostics[0].DuplicateCase);
        
        // 網羅性エラーもある（BとCが不足）
        var exhaustiveDiagnostics = diagnosticsArray.OfType<SwitchCasesNotExhaustiveDiagnostics>().ToArray();
        Assert.Single(exhaustiveDiagnostics);
        Assert.Contains("B", exhaustiveDiagnostics[0].MissingCases);
        Assert.Contains("C", exhaustiveDiagnostics[0].MissingCases);
    }
}
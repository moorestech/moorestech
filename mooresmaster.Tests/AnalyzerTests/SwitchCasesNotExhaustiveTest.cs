using System.Linq;
using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

public class SwitchCasesNotExhaustiveTest
{
    /// <summary>
    ///     switchが全てのenumオプションを網羅している場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void ExhaustiveSwitchTest()
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

        // SwitchCasesNotExhaustiveDiagnosticsが出ていないことを確認
        Assert.DoesNotContain(diagnosticsArray, d => d is SwitchCasesNotExhaustiveDiagnostics);
    }

    /// <summary>
    ///     switchがenumオプションを1つ網羅していない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void MissingOneCaseTest()
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
                        type: number
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray.OfType<SwitchCasesNotExhaustiveDiagnostics>());
        var diagnostics = Assert.IsType<SwitchCasesNotExhaustiveDiagnostics>(diagnosticsArray.First(d => d is SwitchCasesNotExhaustiveDiagnostics));
        Assert.Single(diagnostics.MissingCases);
        Assert.Equal("C", diagnostics.MissingCases[0]);
        Assert.Equal(3, diagnostics.AllEnumOptions.Length);
    }

    /// <summary>
    ///     switchがenumオプションを複数網羅していない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void MissingMultipleCasesTest()
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
                  - D
              - key: data
                switch: ./type
                cases:
                  - when: A
                    type: object
                    properties:
                      - key: valueA
                        type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray.OfType<SwitchCasesNotExhaustiveDiagnostics>());
        var diagnostics = Assert.IsType<SwitchCasesNotExhaustiveDiagnostics>(diagnosticsArray.First(d => d is SwitchCasesNotExhaustiveDiagnostics));
        Assert.Equal(3, diagnostics.MissingCases.Length);
        Assert.Contains("B", diagnostics.MissingCases);
        Assert.Contains("C", diagnostics.MissingCases);
        Assert.Contains("D", diagnostics.MissingCases);
    }

    /// <summary>
    ///     switchの対象がenumでない場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void NonEnumTargetTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: type
                type: string
              - key: data
                switch: ./type
                cases:
                  - when: A
                    type: object
                    properties:
                      - key: valueA
                        type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        // SwitchCasesNotExhaustiveDiagnosticsが出ていないことを確認
        Assert.DoesNotContain(diagnosticsArray, d => d is SwitchCasesNotExhaustiveDiagnostics);
    }

    /// <summary>
    ///     複数のswitchで網羅性エラーがある場合のテスト
    /// </summary>
    [Fact]
    public void MultipleSwitchesNotExhaustiveTest()
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
                  - Z
              - key: data1
                switch: ./type1
                cases:
                  - when: A
                    type: object
                    properties:
                      - key: value
                        type: string
              - key: data2
                switch: ./type2
                cases:
                  - when: X
                    type: object
                    properties:
                      - key: value
                        type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var exhaustiveDiagnostics = diagnosticsArray.OfType<SwitchCasesNotExhaustiveDiagnostics>().ToArray();

        Assert.Equal(2, exhaustiveDiagnostics.Length);

        var diag1 = exhaustiveDiagnostics.First(d => d.AllEnumOptions.Contains("A"));
        Assert.Single(diag1.MissingCases);
        Assert.Equal("B", diag1.MissingCases[0]);

        var diag2 = exhaustiveDiagnostics.First(d => d.AllEnumOptions.Contains("X"));
        Assert.Equal(2, diag2.MissingCases.Length);
        Assert.Contains("Y", diag2.MissingCases);
        Assert.Contains("Z", diag2.MissingCases);
    }

    /// <summary>
    ///     ネストされたswitchでの網羅性チェックのテスト
    /// </summary>
    [Fact]
    public void NestedSwitchNotExhaustiveTest()
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
                          - Z
                      - key: innerData
                        switch: ./innerType
                        cases:
                          - when: X
                            type: object
                            properties:
                              - key: value
                                type: string
                  - when: B
                    type: object
                    properties:
                      - key: value
                        type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var exhaustiveDiagnostics = diagnosticsArray.OfType<SwitchCasesNotExhaustiveDiagnostics>().ToArray();

        Assert.Single(exhaustiveDiagnostics);
        var diagnostics = exhaustiveDiagnostics[0];
        Assert.Equal(2, diagnostics.MissingCases.Length);
        Assert.Contains("Y", diagnostics.MissingCases);
        Assert.Contains("Z", diagnostics.MissingCases);
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
                      - key: value
                        type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var diagnostics = diagnosticsArray.OfType<SwitchCasesNotExhaustiveDiagnostics>().First();

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
                      - key: value
                        type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var diagnostics = diagnosticsArray.OfType<SwitchCasesNotExhaustiveDiagnostics>().First();

        Assert.Contains("B", diagnostics.Message);
        Assert.Contains("not exhaustive", diagnostics.Message);
    }

    /// <summary>
    ///     絶対パスでの網羅性チェックのテスト
    /// </summary>
    [Fact]
    public void AbsolutePathExhaustiveTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: rootType
                type: enum
                options:
                  - A
                  - B
                  - C
              - key: nested
                type: object
                properties:
                  - key: data
                    switch: /rootType
                    cases:
                      - when: A
                        type: object
                        properties:
                          - key: value
                            type: string
                      - when: B
                        type: object
                        properties:
                          - key: value
                            type: number
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var exhaustiveDiagnostics = diagnosticsArray.OfType<SwitchCasesNotExhaustiveDiagnostics>().ToArray();

        Assert.Single(exhaustiveDiagnostics);
        Assert.Single(exhaustiveDiagnostics[0].MissingCases);
        Assert.Equal("C", exhaustiveDiagnostics[0].MissingCases[0]);
    }

    /// <summary>
    ///     親パスでの網羅性チェックのテスト
    /// </summary>
    [Fact]
    public void ParentPathExhaustiveTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: parentType
                type: enum
                options:
                  - A
                  - B
              - key: child
                type: object
                properties:
                  - key: data
                    switch: ./../parentType
                    cases:
                      - when: A
                        type: object
                        properties:
                          - key: value
                            type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        var exhaustiveDiagnostics = diagnosticsArray.OfType<SwitchCasesNotExhaustiveDiagnostics>().ToArray();

        Assert.Single(exhaustiveDiagnostics);
        Assert.Single(exhaustiveDiagnostics[0].MissingCases);
        Assert.Equal("B", exhaustiveDiagnostics[0].MissingCases[0]);
    }
}

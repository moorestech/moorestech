using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     switch関連のDiagnosticsのテスト
/// </summary>
public class SwitchDiagnosticsTest
{
    /// <summary>
    ///     switchの値がJsonStringでない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void SwitchKeyNotStringTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                switch:
                  - notAString
                cases:
                  - when: value1
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<SwitchKeyNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myProperty", diagnostics.PropertyName);
    }

    /// <summary>
    ///     switchパスが不正な場合のエラーのテスト（/または./で始まらない）
    /// </summary>
    [Fact]
    public void InvalidSwitchPathTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                switch: invalidPath
                cases:
                  - when: value1
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<InvalidSwitchPathDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myProperty", diagnostics.PropertyName);
        Assert.Equal("invalidPath", diagnostics.Path);
    }

    /// <summary>
    ///     正常なswitchパスの場合はエラーが出ないテスト（絶対パス）
    /// </summary>
    [Fact]
    public void ValidSwitchPathTest_Absolute()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: switchKey
                type: string
                enum:
                  - value1
                  - value2
              - key: myProperty
                switch: /switchKey
                cases:
                  - when: value1
                    type: object
                    properties:
                      - key: data1
                        type: string
                  - when: value2
                    type: object
                    properties:
                      - key: data2
                        type: number
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is InvalidSwitchPathDiagnostics);
    }

    /// <summary>
    ///     正常なswitchパスの場合はエラーが出ないテスト（相対パス）
    /// </summary>
    [Fact]
    public void ValidSwitchPathTest_Relative()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: switchKey
                type: string
                enum:
                  - value1
                  - value2
              - key: myProperty
                switch: ./switchKey
                cases:
                  - when: value1
                    type: object
                    properties:
                      - key: data1
                        type: string
                  - when: value2
                    type: object
                    properties:
                      - key: data2
                        type: number
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is InvalidSwitchPathDiagnostics);
    }

    /// <summary>
    ///     cases配列の要素がJsonObjectでない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void SwitchCaseNotObjectTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: switchKey
                type: string
              - key: myProperty
                switch: /switchKey
                cases:
                  - notAnObject
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<SwitchCaseNotObjectDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myProperty", diagnostics.PropertyName);
        Assert.Equal(0, diagnostics.Index);
    }

    /// <summary>
    ///     caseのwhenが存在しない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void SwitchCaseWhenMissingTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: switchKey
                type: string
              - key: myProperty
                switch: /switchKey
                cases:
                  - type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<SwitchCaseWhenNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myProperty", diagnostics.PropertyName);
        Assert.Equal(0, diagnostics.Index);
        Assert.Null(diagnostics.ActualWhenNode);
    }

    /// <summary>
    ///     caseのwhenがJsonStringでない場合のエラーのテスト
    /// </summary>
    [Fact]
    public void SwitchCaseWhenNotStringTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: switchKey
                type: string
              - key: myProperty
                switch: /switchKey
                cases:
                  - when: 123
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<SwitchCaseWhenNotStringDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("myProperty", diagnostics.PropertyName);
        Assert.Equal(0, diagnostics.Index);
        Assert.NotNull(diagnostics.ActualWhenNode);
    }

    /// <summary>
    ///     複数のcaseでエラーがある場合のテスト
    /// </summary>
    [Fact]
    public void SwitchMultipleCaseErrorsTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: switchKey
                type: string
              - key: myProperty
                switch: /switchKey
                cases:
                  - when: 111
                    type: string
                  - when: 222
                    type: integer
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Equal(2, diagnosticsArray.Count);
        var diag1 = Assert.IsType<SwitchCaseWhenNotStringDiagnostics>(diagnosticsArray[0]);
        var diag2 = Assert.IsType<SwitchCaseWhenNotStringDiagnostics>(diagnosticsArray[1]);
        Assert.Equal(0, diag1.Index);
        Assert.Equal(1, diag2.Index);
    }

    /// <summary>
    ///     正常な場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void ValidSwitchTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: switchKey
                type: string
                enum:
                  - value1
                  - value2
              - key: myProperty
                switch: /switchKey
                cases:
                  - when: value1
                    type: object
                    properties:
                      - key: data1
                        type: string
                  - when: value2
                    type: object
                    properties:
                      - key: data2
                        type: number
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is SwitchKeyNotStringDiagnostics);
        Assert.DoesNotContain(diagnosticsArray, d => d is InvalidSwitchPathDiagnostics);
        Assert.DoesNotContain(diagnosticsArray, d => d is SwitchCaseNotObjectDiagnostics);
        Assert.DoesNotContain(diagnosticsArray, d => d is SwitchCaseWhenNotStringDiagnostics);
    }
}

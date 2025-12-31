using System;
using mooresmaster.Generator.Analyze.Analyzers;
using Xunit;
using Xunit.Abstractions;

namespace mooresmaster.Tests.AnalyzerTests;

public class SwitchPathAnalyzerTest
{
  private readonly ITestOutputHelper _testOutputHelper;
  
  public SwitchPathAnalyzerTest(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
  }
  
  /// <summary>
    ///     相対パスで存在しないプロパティを参照した場合のエラーのテスト
    /// </summary>
    [Fact]
    public void RelativePathPropertyNotFound()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: blockType
                type: string
                enum:
                  - TypeA
                  - TypeB
              - key: config
                switch: ./nonExistentProperty
                cases:
                  - when: TypeA
                    type: object
                    properties: []
                  - when: TypeB
                    type: object
                    properties: []
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        _testOutputHelper.WriteLine(Test.Generate(schema).analysis.ToString());

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<SwitchPathPropertyNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("nonExistentProperty", diagnostics.PropertyName);
        Assert.Contains("blockType", diagnostics.AvailableProperties);
        Assert.Contains("config", diagnostics.AvailableProperties);
    }

    /// <summary>
    ///     絶対パスで存在しないプロパティを参照した場合のエラーのテスト
    /// </summary>
    [Fact]
    public void AbsolutePathPropertyNotFound()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: data0
                type: object
                properties:
                  - key: data1
                    switch: /nonExistentProperty/data2
                    cases:
                      - when: Moores
                        type: object
                        properties: []
                  - key: data2
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<SwitchPathPropertyNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("nonExistentProperty", diagnostics.PropertyName);
        Assert.Contains("data0", diagnostics.AvailableProperties);
    }

    /// <summary>
    ///     非オブジェクト型に対してプロパティアクセスを試みた場合のエラーのテスト
    /// </summary>
    [Fact]
    public void AccessPropertyOnNonObject()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: stringProp
                type: string
              - key: config
                switch: ./stringProp/invalidPath
                cases:
                  - when: TypeA
                    type: object
                    properties: []
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<SwitchPathNotAnObjectDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("invalidPath", diagnostics.PropertyName);
    }

    /// <summary>
    ///     正しい相対パスの場合はエラーが出ないことのテスト
    /// </summary>
    [Fact]
    public void ValidRelativePathNoError()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: blockType
                type: string
                enum:
                  - TypeA
                  - TypeB
              - key: config
                switch: ./blockType
                cases:
                  - when: TypeA
                    type: object
                    properties: []
                  - when: TypeB
                    type: object
                    properties: []
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Empty(diagnosticsArray);
    }

    /// <summary>
    ///     正しい絶対パスの場合はエラーが出ないことのテスト
    /// </summary>
    [Fact]
    public void ValidAbsolutePathNoError()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: data0
                type: object
                properties:
                  - key: data1
                    switch: /data0/data2
                    cases:
                      - when: Moores
                        type: object
                        properties: []
                      - when: Tech
                        type: object
                        properties: []
                  - key: data2
                    type: string
                    enum:
                      - Moores
                      - Tech
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Empty(diagnosticsArray);
    }

    /// <summary>
    ///     利用可能なプロパティ一覧が正しく取得できることのテスト
    /// </summary>
    [Fact]
    public void AvailablePropertiesAreCorrect()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: propertyA
                type: string
              - key: propertyB
                type: integer
              - key: propertyC
                type: boolean
              - key: config
                switch: ./nonExistent
                cases:
                  - when: TypeA
                    type: object
                    properties: []
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<SwitchPathPropertyNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal(4, diagnostics.AvailableProperties.Length);
        Assert.Contains("propertyA", diagnostics.AvailableProperties);
        Assert.Contains("propertyB", diagnostics.AvailableProperties);
        Assert.Contains("propertyC", diagnostics.AvailableProperties);
        Assert.Contains("config", diagnostics.AvailableProperties);
    }
}

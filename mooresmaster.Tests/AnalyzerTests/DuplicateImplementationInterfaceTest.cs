using System;
using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace mooresmaster.Tests.AnalyzerTests;

public class DuplicateImplementationInterfaceTest
{
  private readonly ITestOutputHelper _testOutputHelper;
  
  public DuplicateImplementationInterfaceTest(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
  }
  
  /// <summary>
    ///     DefineInterfaceで同じインターフェースを2回継承した場合のエラーのテスト
    /// </summary>
    [Fact]
    public void DefineInterfaceDuplicateImplementation()
    {
        const string schema =
            """
            id: testSchema
            type: object

            defineInterface:
              - interfaceName: IParent
                properties:
                  - key: test0
                    type: integer
              - interfaceName: IChild
                implementationInterface:
                  - IParent
                  - IParent
                properties:
                  - key: test1
                    type: integer
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        _testOutputHelper.WriteLine(Test.Generate(schema).analysis.ToString());

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DuplicateImplementationInterfaceDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IChild", diagnostics.TargetName);
        Assert.Equal("IParent", diagnostics.DuplicateInterfaceName);
        Assert.Equal(2, diagnostics.Locations.Length);
    }

    /// <summary>
    ///     DefineInterfaceで同じインターフェースを3回継承した場合のエラーのテスト
    /// </summary>
    [Fact]
    public void DefineInterfaceTriplicateImplementation()
    {
        const string schema =
            """
            id: testSchema
            type: object

            defineInterface:
              - interfaceName: IParent
                properties:
                  - key: test0
                    type: integer
              - interfaceName: IChild
                implementationInterface:
                  - IParent
                  - IParent
                  - IParent
                properties:
                  - key: test1
                    type: integer
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DuplicateImplementationInterfaceDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IChild", diagnostics.TargetName);
        Assert.Equal("IParent", diagnostics.DuplicateInterfaceName);
        Assert.Equal(3, diagnostics.Locations.Length);
    }

    /// <summary>
    ///     ObjectSchemaで同じインターフェースを2回継承した場合のエラーのテスト
    /// </summary>
    [Fact]
    public void ObjectSchemaDuplicateImplementation()
    {
        const string schema =
            """
            id: testSchema
            type: object

            defineInterface:
              - interfaceName: IParent
                properties:
                  - key: test0
                    type: integer

            properties:
              - key: child
                type: object
                implementationInterface:
                  - IParent
                  - IParent
                properties:
                  - key: test0
                    type: integer
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DuplicateImplementationInterfaceDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("child", diagnostics.TargetName);
        Assert.Equal("IParent", diagnostics.DuplicateInterfaceName);
        Assert.Equal(2, diagnostics.Locations.Length);
    }

    /// <summary>
    ///     重複がない場合はエラーが発生しないことの確認
    /// </summary>
    [Fact]
    public void NoDuplicateImplementation()
    {
        const string schema =
            """
            id: testSchema
            type: object

            defineInterface:
              - interfaceName: IParent1
                properties:
                  - key: test0
                    type: integer
              - interfaceName: IParent2
                properties:
                  - key: test1
                    type: integer
              - interfaceName: IChild
                implementationInterface:
                  - IParent1
                  - IParent2
                properties:
                  - key: test2
                    type: integer
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Empty(diagnosticsArray);
    }
}

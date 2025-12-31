using System;
using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace mooresmaster.Tests.AnalyzerTests;

public class DuplicateInterfaceNameTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    
    public DuplicateInterfaceNameTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }
    
    /// <summary>
    ///     同名のdefineInterfaceが複数定義されている場合のエラーのテスト
    /// </summary>
    [Fact]
    public void DuplicateInterfaceNameInSameSchemaTest()
    {
        const string schema =
            """
            id: testSchema
            type: object

            defineInterface:
              - interfaceName: IDuplicateInterface
                properties:
                  - key: field1
                    type: string
              - interfaceName: IDuplicateInterface
                properties:
                  - key: field2
                    type: integer
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DuplicateInterfaceNameDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IDuplicateInterface", diagnostics.InterfaceName);
    }

    /// <summary>
    ///     異なるスキーマファイル間で同名のdefineInterfaceが定義されている場合のエラーのテスト
    /// </summary>
    [Fact]
    public void DuplicateInterfaceNameAcrossFilesTest()
    {
        const string schema1 =
            """
            id: testSchema1
            type: object

            defineInterface:
              - interfaceName: IDuplicateInterface
                properties:
                  - key: field1
                    type: string
            """;

        const string schema2 =
            """
            id: testSchema2
            type: object

            defineInterface:
              - interfaceName: IDuplicateInterface
                properties:
                  - key: field2
                    type: integer
            """;

        var diagnosticsArray = Test.Generate(schema1, schema2).analysis.DiagnosticsList;

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DuplicateInterfaceNameDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IDuplicateInterface", diagnostics.InterfaceName);
    }

    /// <summary>
    ///     異なるスキーマファイル間でglobalDefineInterfaceの同名が定義されている場合のエラーのテスト
    /// </summary>
    [Fact]
    public void DuplicateGlobalInterfaceNameAcrossFilesTest()
    {
        const string schema1 =
            """
            id: testSchema1
            type: object

            globalDefineInterface:
              - interfaceName: IGlobalDuplicateInterface
                properties:
                  - key: field1
                    type: string
            """;

        const string schema2 =
            """
            id: testSchema2
            type: object

            globalDefineInterface:
              - interfaceName: IGlobalDuplicateInterface
                properties:
                  - key: field2
                    type: integer
            """;

        var diagnosticsArray = Test.Generate(schema1, schema2).analysis.DiagnosticsList;
        _testOutputHelper.WriteLine(Test.Generate(schema1, schema2).analysis.ToString());

        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<DuplicateInterfaceNameDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("IGlobalDuplicateInterface", diagnostics.InterfaceName);
    }

    /// <summary>
    ///     異なる名前のインターフェースが重複しないことのテスト
    /// </summary>
    [Fact]
    public void NoDuplicateInterfaceNameTest()
    {
        const string schema =
            """
            id: testSchema
            type: object

            defineInterface:
              - interfaceName: IInterface1
                properties:
                  - key: field1
                    type: string
              - interfaceName: IInterface2
                properties:
                  - key: field2
                    type: integer
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.Empty(diagnosticsArray);
    }
}

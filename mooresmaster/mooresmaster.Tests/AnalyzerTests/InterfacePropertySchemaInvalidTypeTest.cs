using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

/// <summary>
///     defineInterfaceのpropertyで有効な型を使った場合のテスト
///     注: SwitchSchemaはIDefineInterfacePropertySchemaを実装していないが、
///     defineInterfaceのプロパティでswitch型を使おうとするとParseSwitchで例外が発生するため、
///     InterfacePropertySchemaInvalidTypeDiagnosticsは現状トリガーされない。
///     将来、新しいスキーマ型が追加された場合のために、このDiagnosticsクラスは残しておく。
/// </summary>
public class InterfacePropertySchemaInvalidTypeTest
{
    /// <summary>
    ///     正常な型（string）の場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void InterfacePropertySchemaValidTypeTest_String()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName: IMyInterface
                properties:
                  - key: validProp
                    type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is InterfacePropertySchemaInvalidTypeDiagnostics);
    }

    /// <summary>
    ///     正常な型（integer）の場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void InterfacePropertySchemaValidTypeTest_Integer()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName: IMyInterface
                properties:
                  - key: validProp
                    type: integer
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is InterfacePropertySchemaInvalidTypeDiagnostics);
    }

    /// <summary>
    ///     正常な型（boolean）の場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void InterfacePropertySchemaValidTypeTest_Boolean()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName: IMyInterface
                properties:
                  - key: validProp
                    type: boolean
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is InterfacePropertySchemaInvalidTypeDiagnostics);
    }

    /// <summary>
    ///     正常な型（object）の場合はエラーが出ないテスト
    ///     ObjectSchemaはIDefineInterfacePropertySchemaを実装している
    /// </summary>
    [Fact]
    public void InterfacePropertySchemaValidTypeTest_Object()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName: IMyInterface
                properties:
                  - key: validProp
                    type: object
                    properties:
                      - key: nested
                        type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is InterfacePropertySchemaInvalidTypeDiagnostics);
    }

    /// <summary>
    ///     正常な型（array）の場合はエラーが出ないテスト
    ///     ArraySchemaはIDefineInterfacePropertySchemaを実装している
    /// </summary>
    [Fact]
    public void InterfacePropertySchemaValidTypeTest_Array()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName: IMyInterface
                properties:
                  - key: validProp
                    type: array
                    items:
                      type: string
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is InterfacePropertySchemaInvalidTypeDiagnostics);
    }

    /// <summary>
    ///     正常な型（number）の場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void InterfacePropertySchemaValidTypeTest_Number()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName: IMyInterface
                properties:
                  - key: validProp
                    type: number
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is InterfacePropertySchemaInvalidTypeDiagnostics);
    }

    /// <summary>
    ///     正常な型（uuid）の場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void InterfacePropertySchemaValidTypeTest_Uuid()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: myProperty
                type: string
            defineInterface:
              - interfaceName: IMyInterface
                properties:
                  - key: validProp
                    type: uuid
            """;

        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;

        Assert.DoesNotContain(diagnosticsArray, d => d is InterfacePropertySchemaInvalidTypeDiagnostics);
    }
}

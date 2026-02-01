using System.Linq;
using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

public class RefAnalyzerTest
{
    #region RefNotFound Tests
    
    /// <summary>
    ///     存在しないスキーマを参照した場合のエラーのテスト
    /// </summary>
    [Fact]
    public void RefNotFoundTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: user
                ref: nonExistentSchema
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RefNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("nonExistentSchema", diagnostics.RefSchema.Ref);
        Assert.Equal("user", diagnostics.RefSchema.PropertyName);
        Assert.Single(diagnostics.Locations);
    }
    
    /// <summary>
    ///     存在するスキーマを参照した場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void RefFoundTest()
    {
        const string schema1 =
            """
            id: userSchema
            type: object
            properties:
              - key: name
                type: string
            """;
        
        const string schema2 =
            """
            id: postSchema
            type: object
            properties:
              - key: author
                ref: userSchema
            """;
        
        var diagnosticsArray = Test.Generate(schema1, schema2).analysis.DiagnosticsList;
        
        Assert.Empty(diagnosticsArray);
    }
    
    /// <summary>
    ///     複数の存在しないスキーマを参照した場合のエラーのテスト
    /// </summary>
    [Fact]
    public void MultipleRefNotFoundTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: user
                ref: nonExistentUser
              - key: post
                ref: nonExistentPost
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Equal(2, diagnosticsArray.Count);
        
        var diagnostics1 = Assert.IsType<RefNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("nonExistentUser", diagnostics1.RefSchema.Ref);
        
        var diagnostics2 = Assert.IsType<RefNotFoundDiagnostics>(diagnosticsArray[1]);
        Assert.Equal("nonExistentPost", diagnostics2.RefSchema.Ref);
    }
    
    /// <summary>
    ///     配列内のRefで存在しないスキーマを参照した場合のエラーのテスト
    /// </summary>
    [Fact]
    public void RefNotFoundInArrayTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: users
                type: array
                items:
                  ref: nonExistentUser
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RefNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("nonExistentUser", diagnostics.RefSchema.Ref);
    }
    
    /// <summary>
    ///     ネストされたオブジェクト内のRefで存在しないスキーマを参照した場合のエラーのテスト
    /// </summary>
    [Fact]
    public void RefNotFoundInNestedObjectTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: data
                type: object
                properties:
                  - key: user
                    ref: nonExistentUser
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RefNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("nonExistentUser", diagnostics.RefSchema.Ref);
    }
    
    /// <summary>
    ///     switchのcase内のRefで存在しないスキーマを参照した場合のエラーのテスト
    /// </summary>
    [Fact]
    public void RefNotFoundInSwitchCaseTest()
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
                      - key: user
                        ref: nonExistentUser
                  - when: B
                    type: object
                    properties:
                      - key: name
                        type: string
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RefNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("nonExistentUser", diagnostics.RefSchema.Ref);
    }
    
    /// <summary>
    ///     利用可能なスキーマIDがDiagnosticsに含まれていることのテスト
    /// </summary>
    [Fact]
    public void RefNotFoundContainsAvailableSchemasTest()
    {
        const string schema1 =
            """
            id: schemaA
            type: object
            properties:
              - key: name
                type: string
            """;
        
        const string schema2 =
            """
            id: schemaB
            type: object
            properties:
              - key: ref
                ref: nonExistent
            """;
        
        var diagnosticsArray = Test.Generate(schema1, schema2).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RefNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Contains("schemaA", diagnostics.AvailableSchemaIds);
        Assert.Contains("schemaB", diagnostics.AvailableSchemaIds);
    }
    
    #endregion
    
    #region CircularRef Tests
    
    /// <summary>
    ///     自己参照（A -> A）の循環参照のテスト
    /// </summary>
    [Fact]
    public void SelfCircularRefTest()
    {
        const string schema =
            """
            id: schemaA
            type: object
            properties:
              - key: self
                ref: schemaA
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<CircularRefDiagnostics>(diagnosticsArray[0]);
        Assert.Equal("schemaA", diagnostics.RefSchema.Ref);
        Assert.Contains("schemaA", diagnostics.CircularPath);
    }
    
    /// <summary>
    ///     2つのスキーマ間の循環参照（A -> B -> A）のテスト
    /// </summary>
    [Fact]
    public void TwoSchemaCircularRefTest()
    {
        const string schemaA =
            """
            id: schemaA
            type: object
            properties:
              - key: refB
                ref: schemaB
            """;
        
        const string schemaB =
            """
            id: schemaB
            type: object
            properties:
              - key: refA
                ref: schemaA
            """;
        
        var diagnosticsArray = Test.Generate(schemaA, schemaB).analysis.DiagnosticsList;
        
        // A -> B -> A で循環参照が検出される
        Assert.NotEmpty(diagnosticsArray);
        var circularDiagnostics = diagnosticsArray.OfType<CircularRefDiagnostics>().ToList();
        Assert.NotEmpty(circularDiagnostics);
    }
    
    /// <summary>
    ///     3つのスキーマ間の循環参照（A -> B -> C -> A）のテスト
    /// </summary>
    [Fact]
    public void ThreeSchemaCircularRefTest()
    {
        const string schemaA =
            """
            id: schemaA
            type: object
            properties:
              - key: refB
                ref: schemaB
            """;
        
        const string schemaB =
            """
            id: schemaB
            type: object
            properties:
              - key: refC
                ref: schemaC
            """;
        
        const string schemaC =
            """
            id: schemaC
            type: object
            properties:
              - key: refA
                ref: schemaA
            """;
        
        var diagnosticsArray = Test.Generate(schemaA, schemaB, schemaC).analysis.DiagnosticsList;
        
        Assert.NotEmpty(diagnosticsArray);
        var circularDiagnostics = diagnosticsArray.OfType<CircularRefDiagnostics>().ToList();
        Assert.NotEmpty(circularDiagnostics);
    }
    
    /// <summary>
    ///     循環参照がない場合はエラーが出ないテスト
    /// </summary>
    [Fact]
    public void NoCircularRefTest()
    {
        const string schemaA =
            """
            id: schemaA
            type: object
            properties:
              - key: name
                type: string
            """;
        
        const string schemaB =
            """
            id: schemaB
            type: object
            properties:
              - key: refA
                ref: schemaA
            """;
        
        const string schemaC =
            """
            id: schemaC
            type: object
            properties:
              - key: refA
                ref: schemaA
              - key: refB
                ref: schemaB
            """;
        
        var diagnosticsArray = Test.Generate(schemaA, schemaB, schemaC).analysis.DiagnosticsList;
        
        Assert.Empty(diagnosticsArray);
    }
    
    /// <summary>
    ///     配列内のRefで循環参照が発生した場合のテスト
    /// </summary>
    [Fact]
    public void CircularRefInArrayTest()
    {
        const string schemaA =
            """
            id: schemaA
            type: object
            properties:
              - key: items
                type: array
                items:
                  ref: schemaB
            """;
        
        const string schemaB =
            """
            id: schemaB
            type: object
            properties:
              - key: parent
                ref: schemaA
            """;
        
        var diagnosticsArray = Test.Generate(schemaA, schemaB).analysis.DiagnosticsList;
        
        Assert.NotEmpty(diagnosticsArray);
        var circularDiagnostics = diagnosticsArray.OfType<CircularRefDiagnostics>().ToList();
        Assert.NotEmpty(circularDiagnostics);
    }
    
    /// <summary>
    ///     switchのcase内のRefで循環参照が発生した場合のテスト
    /// </summary>
    [Fact]
    public void CircularRefInSwitchCaseTest()
    {
        const string schemaA =
            """
            id: schemaA
            type: object
            properties:
              - key: type
                type: enum
                options:
                  - X
                  - Y
              - key: data
                switch: ./type
                cases:
                  - when: X
                    type: object
                    properties:
                      - key: refB
                        ref: schemaB
                  - when: Y
                    type: object
                    properties:
                      - key: name
                        type: string
            """;
        
        const string schemaB =
            """
            id: schemaB
            type: object
            properties:
              - key: refA
                ref: schemaA
            """;
        
        var diagnosticsArray = Test.Generate(schemaA, schemaB).analysis.DiagnosticsList;
        
        Assert.NotEmpty(diagnosticsArray);
        var circularDiagnostics = diagnosticsArray.OfType<CircularRefDiagnostics>().ToList();
        Assert.NotEmpty(circularDiagnostics);
    }
    
    /// <summary>
    ///     循環参照のパスが正しく報告されることのテスト
    /// </summary>
    [Fact]
    public void CircularRefPathTest()
    {
        const string schema =
            """
            id: selfRef
            type: object
            properties:
              - key: self
                ref: selfRef
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<CircularRefDiagnostics>(diagnosticsArray[0]);
        Assert.Equal(2, diagnostics.CircularPath.Length);
        Assert.Equal("selfRef", diagnostics.CircularPath[0]);
        Assert.Equal("selfRef", diagnostics.CircularPath[1]);
    }
    
    #endregion
    
    #region Location Tests
    
    /// <summary>
    ///     RefNotFoundDiagnosticsのLocationが正しいことのテスト
    /// </summary>
    [Fact]
    public void RefNotFoundLocationTest()
    {
        const string schema =
            """
            id: testSchema
            type: object
            properties:
              - key: user
                ref: nonExistent
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<RefNotFoundDiagnostics>(diagnosticsArray[0]);
        Assert.Single(diagnostics.Locations);
        // Locationが存在することを確認
        Assert.True(diagnostics.Locations[0].StartLine > 0 || diagnostics.Locations[0].StartColumn > 0);
    }
    
    /// <summary>
    ///     CircularRefDiagnosticsのLocationが正しいことのテスト
    /// </summary>
    [Fact]
    public void CircularRefLocationTest()
    {
        const string schema =
            """
            id: selfRef
            type: object
            properties:
              - key: self
                ref: selfRef
            """;
        
        var diagnosticsArray = Test.Generate(schema).analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsArray);
        var diagnostics = Assert.IsType<CircularRefDiagnostics>(diagnosticsArray[0]);
        Assert.Single(diagnostics.Locations);
        // Locationが存在することを確認
        Assert.True(diagnostics.Locations[0].StartLine > 0 || diagnostics.Locations[0].StartColumn > 0);
    }
    
    #endregion
}
using System.Linq;
using mooresmaster.Generator.CodeGenerate;
using mooresmaster.Generator.LoaderGenerate;
using Mooresmaster.Loader.ArrayIndexTestModule;
using Mooresmaster.Model.ArrayIndexTestModule;
using Newtonsoft.Json.Linq;
using Xunit;

namespace mooresmaster.Tests.ArrayIndexTests;

public class ArrayIndexTest
{
    [Fact]
    public void ArrayInnerTypeHasIndexProperty()
    {
        var yaml = """
                   id: test
                   type: object
                   properties:
                   - key: items
                     type: array
                     items:
                       type: object
                       properties:
                       - key: name
                         type: string
                   """;

        var (schemaTable, nameTable, semantics, definition) = Test.Generate(yaml);
        var codeFiles = CodeGenerator.Generate(definition, semantics);
        var loaderFiles = LoaderGenerator.Generate(definition, semantics, nameTable);

        // 配列のInnerTypeのコードを確認
        var itemsElementCode = codeFiles.FirstOrDefault(f => f.Code.Contains("class ItemsElement"));
        Assert.NotNull(itemsElementCode);

        // Indexプロパティが生成されているか確認
        Assert.Contains("public int Index { get; }", itemsElementCode.Code);

        // コンストラクタにIndexが含まれているか確認
        Assert.Contains("int Index", itemsElementCode.Code);
        Assert.Contains("this.Index = Index;", itemsElementCode.Code);

        // ItemsElementLoaderがIndexパラメータを受け取るか確認
        var itemsElementLoaderCode = loaderFiles.FirstOrDefault(f => f.Code.Contains("ItemsElementLoader"));
        Assert.NotNull(itemsElementLoaderCode);
        Assert.Contains("Load(int Index,", itemsElementLoaderCode.Code);

        // TestLoaderでSelect with Indexが使われているか確認
        var testLoaderCode = loaderFiles.FirstOrDefault(f => f.Code.Contains("TestLoader"));
        Assert.NotNull(testLoaderCode);
        Assert.Contains("(value, __Index__)", testLoaderCode.Code);

        // Loaderが__Index__をLoad関数に渡しているか確認
        Assert.Contains("Load(__Index__, value)", testLoaderCode.Code);

        // ItemsElementLoaderがIndexをコンストラクタに渡しているか確認
        Assert.Contains("return new", itemsElementLoaderCode.Code);
        Assert.Contains("(Index,", itemsElementLoaderCode.Code);
    }

    [Fact]
    public void ArrayInnerTypeIndexIsAssignedCorrectly()
    {
        // 生成されたコードの構造を詳細に確認するテスト
        var yaml = """
                   id: test
                   type: object
                   properties:
                   - key: data
                     type: array
                     items:
                       type: object
                       properties:
                       - key: id
                         type: integer
                       - key: value
                         type: string
                   """;

        var (schemaTable, nameTable, semantics, definition) = Test.Generate(yaml);
        var codeFiles = CodeGenerator.Generate(definition, semantics);
        var loaderFiles = LoaderGenerator.Generate(definition, semantics, nameTable);

        // Model側: DataElementクラスにIndexプロパティがある
        var dataElementCode = codeFiles.FirstOrDefault(f => f.Code.Contains("class DataElement"));
        Assert.NotNull(dataElementCode);
        Assert.Contains("public int Index { get; }", dataElementCode.Code);

        // Model側: コンストラクタの最初の引数がIndex
        Assert.Contains("public DataElement(int Index,", dataElementCode.Code);
        Assert.Contains("this.Index = Index;", dataElementCode.Code);

        // Loader側: DataElementLoaderのLoad関数がint Indexを最初の引数として受け取る
        var dataElementLoader = loaderFiles.FirstOrDefault(f => f.Code.Contains("DataElementLoader"));
        Assert.NotNull(dataElementLoader);
        Assert.Contains("public static", dataElementLoader.Code);
        Assert.Contains("Load(int Index, global::Newtonsoft.Json.Linq.JToken json)", dataElementLoader.Code);

        // Loader側: new DataElement(Index, ...) の形式でIndexを渡している
        Assert.Contains("new", dataElementLoader.Code);
        Assert.Contains("(Index, Id, Value)", dataElementLoader.Code);

        // 親Loader側: Select((value, __Index__) => ...) の形式でインデックス付きSelectを使用
        var testLoader = loaderFiles.FirstOrDefault(f => f.Code.Contains("TestLoader"));
        Assert.NotNull(testLoader);
        Assert.Contains("Select(", testLoader.Code);
        Assert.Contains("(value, __Index__)", testLoader.Code);
        Assert.Contains("DataElementLoader.Load(__Index__, value)", testLoader.Code);
    }

    [Fact]
    public void ArrayElementsHaveCorrectIndex()
    {
        // 配列要素を含むJSONデータ
        var json = JToken.Parse("""
            {
                "items": [
                    { "name": "first", "value": 100 },
                    { "name": "second", "value": 200 },
                    { "name": "third", "value": 300 }
                ]
            }
            """);

        // ロード
        var result = ArrayIndexTestLoader.Load(json);

        // 配列の要素数を確認
        Assert.Equal(3, result.Items.Length);

        // 各要素のIndexが配列内の位置と一致することを確認
        Assert.Equal(0, result.Items[0].Index);
        Assert.Equal("first", result.Items[0].Name);
        Assert.Equal(100, result.Items[0].Value);

        Assert.Equal(1, result.Items[1].Index);
        Assert.Equal("second", result.Items[1].Name);
        Assert.Equal(200, result.Items[1].Value);

        Assert.Equal(2, result.Items[2].Index);
        Assert.Equal("third", result.Items[2].Name);
        Assert.Equal(300, result.Items[2].Value);
    }

    [Fact]
    public void EmptyArrayWorks()
    {
        var json = JToken.Parse("""
            {
                "items": []
            }
            """);

        var result = ArrayIndexTestLoader.Load(json);

        Assert.Empty(result.Items);
    }

    [Fact]
    public void SingleElementArrayHasIndexZero()
    {
        var json = JToken.Parse("""
            {
                "items": [
                    { "name": "only", "value": 42 }
                ]
            }
            """);

        var result = ArrayIndexTestLoader.Load(json);

        Assert.Single(result.Items);
        Assert.Equal(0, result.Items[0].Index);
        Assert.Equal("only", result.Items[0].Name);
        Assert.Equal(42, result.Items[0].Value);
    }
}

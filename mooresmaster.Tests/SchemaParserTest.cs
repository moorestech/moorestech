using mooresmaster.Generator.JsonSchema;
using Xunit;

namespace mooresmaster.Tests;

public class SchemaParserTest
{
    [Fact]
    public void SchemaSwitchAbsolutePathTest()
    {
        var path = "/Test/aaa/bbb";

        var switchPath = SwitchPathParser.Parse(path);

        // absoluteか
        Assert.Equal(SwitchPathType.Absolute, switchPath.Type);

        // パスが正しいか
        var collectSwitchPath = new SwitchPath([
                new NormalSwitchPathElement("Test"),
                new NormalSwitchPathElement("aaa"),
                new NormalSwitchPathElement("bbb")
            ],
            SwitchPathType.Absolute
        );

        Assert.Equal(collectSwitchPath, switchPath);
    }

    [Fact]
    public void SchemaSwitchRelativePathTest()
    {
        var path = "./Test/aaa/bbb";

        var switchPath = SwitchPathParser.Parse(path);

        // relativeか
        Assert.Equal(SwitchPathType.Relative, switchPath.Type);

        // パスが正しいか
        var collectSwitchPath = new SwitchPath(
            [
                new NormalSwitchPathElement("Test"),
                new NormalSwitchPathElement("aaa"),
                new NormalSwitchPathElement("bbb")
            ],
            SwitchPathType.Relative
        );

        Assert.Equal(collectSwitchPath, switchPath);
    }

    [Fact]
    public void SchemaSwitchParentPathTest()
    {
        var path = "./Test/../Test/aaa/bbb";

        var switchPath = SwitchPathParser.Parse(path);

        // relativeか
        Assert.Equal(SwitchPathType.Relative, switchPath.Type);

        // パスが正しいか
        var collectSwitchPath = new SwitchPath(
            [
                new NormalSwitchPathElement("Test"),
                new ParentSwitchPathElement(),
                new NormalSwitchPathElement("Test"),
                new NormalSwitchPathElement("aaa"),
                new NormalSwitchPathElement("bbb")
            ],
            SwitchPathType.Relative
        );

        Assert.Equal(collectSwitchPath, switchPath);
    }
}

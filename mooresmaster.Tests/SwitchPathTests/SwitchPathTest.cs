using System;
using mooresmaster.Generator.JsonSchema;
using Mooresmaster.Loader.AbsoluteSwitchPathTestSchemaModule;
using Mooresmaster.Loader.RelativeSwitchPathTestSchemaModule;
using Xunit;

namespace mooresmaster.Tests.SwitchPathTests;

public class SwitchPathTest
{
    [Fact]
    public void RelativeSwitchPathLoaderTest()
    {
        RelativeSwitchPathTestSchemaLoader.Load(Test.GetJson("SwitchPathTests/SwitchPathTestSchema"));
    }

    [Fact]
    public void RelativeSwitchPathLoaderThrowTest()
    {
        Assert.ThrowsAny<Exception>(() => RelativeSwitchPathTestSchemaLoader.Load(Test.GetJson("SwitchPathTests/SwitchPathThrowTestSchema")));
    }

    [Fact]
    public void AbsoluteSwitchPathLoaderTest()
    {
        AbsoluteSwitchPathTestSchemaLoader.Load(Test.GetJson("SwitchPathTests/SwitchPathTestSchema"));
    }

    [Fact]
    public void AbsoluteSwitchPathLoaderThrowTest()
    {
        Assert.ThrowsAny<Exception>(() => AbsoluteSwitchPathTestSchemaLoader.Load(Test.GetJson("SwitchPathTests/SwitchPathThrowTestSchema")));
    }

    [Fact]
    public void SchemaSwitchAbsolutePathParseTest()
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
    public void SchemaSwitchRelativePathParseTest()
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
    public void SchemaSwitchParentPathParseTest()
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

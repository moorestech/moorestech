using Mooresmaster.LocalizationCsv;
using mooresmaster.Generator.Localization;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

public class LocalizationKeyTreeTest
{
    [Fact]
    public void ネスト木を入力順で構築できる()
    {
        var rows = new[]
        {
            new LocalizationRow("ui.buildMenu.close", "", new[] { "" }),
            new LocalizationRow("ui.buildMenu.title", "", new[] { "" }),
            new LocalizationRow("ui.inventory.title", "", new[] { "" }),
        };

        var root = LocalizationKeyTree.Build(rows);

        var ui = Assert.Single(root.Children);
        Assert.Equal("ui", ui.Segment);
        Assert.Equal("ui", ui.FullKey);
        Assert.Equal(2, ui.Children.Count);

        var buildMenu = ui.Children[0];
        Assert.Equal("buildMenu", buildMenu.Segment);
        Assert.Equal("ui.buildMenu", buildMenu.FullKey);
        Assert.False(buildMenu.IsLeaf);
        Assert.Equal(2, buildMenu.Children.Count);
        Assert.Equal("close", buildMenu.Children[0].Segment);
        Assert.Equal("ui.buildMenu.close", buildMenu.Children[0].FullKey);
        Assert.True(buildMenu.Children[0].IsLeaf);
        Assert.Equal("title", buildMenu.Children[1].Segment);
        Assert.Equal("ui.buildMenu.title", buildMenu.Children[1].FullKey);
        Assert.True(buildMenu.Children[1].IsLeaf);

        var inventory = ui.Children[1];
        Assert.Equal("inventory", inventory.Segment);
        Assert.Equal("ui.inventory", inventory.FullKey);
        var inventoryTitle = Assert.Single(inventory.Children);
        Assert.Equal("ui.inventory.title", inventoryTitle.FullKey);
        Assert.True(inventoryTitle.IsLeaf);
    }

    [Fact]
    public void 親から子の順で葉と枝を兼ねるキーは例外()
    {
        var rows = new[]
        {
            new LocalizationRow("ui.save", "", new[] { "" }),
            new LocalizationRow("ui.save.confirm", "", new[] { "" }),
        };

        Assert.Throws<LocalizationCsvException>(() => LocalizationKeyTree.Build(rows));
    }

    [Fact]
    public void 子から親の順で葉と枝を兼ねるキーは例外()
    {
        var rows = new[]
        {
            new LocalizationRow("ui.save.confirm", "", new[] { "" }),
            new LocalizationRow("ui.save", "", new[] { "" }),
        };

        Assert.Throws<LocalizationCsvException>(() => LocalizationKeyTree.Build(rows));
    }

    [Fact]
    public void 空入力は空のルートを返す()
    {
        var root = LocalizationKeyTree.Build([]);

        Assert.Equal("", root.Segment);
        Assert.Equal("", root.FullKey);
        Assert.False(root.IsLeaf);
        Assert.Empty(root.Children);
    }
}

using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Recipe;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Logic;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ThemeTokenFile;
using static Client.Tests.Ui.State.Recipe.RecipeFixtures;

namespace Client.Tests.Ui.Components.Recipe
{
    /// <summary>
    /// 継承バッジの表示・隠し・見た目（L1）
    /// Inheritance badge show, hide and look (L1)
    /// </summary>
    public class LevelInheritanceBadgeTest : UiTestHost
    {
        private const string TestId = "level-inheritance-badge-test";

        [SetUp]
        public void LoadDictionary()
        {
            Localize.Initialize();
        }

        [Test]
        public void 継承の文を渡すとバッジの文を出す()
        {
            // Web と同じ継承（入力 0 → 出力 0・解放済みの上限 2）から文を作って載せる
            // Build the text from the same inheritance as the Web test (input 0 to output 0, unlocked cap 2) and show it
            var inheritance = RecipeLevelInheritance.InheritUpToUnlocked(0, Ids(11, 12, 13), 0, Ids(21, 22, 23), 2);
            Assert.IsTrue(LevelInheritanceBadgeText.TryBuild(inheritance, out var text));
            var badge = new LevelInheritanceBadge(TestId);
            badge.Show(text);
            rootVisualElement.Add(badge);
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.Flex, badge.resolvedStyle.display);
            Assert.AreEqual(text.Resolve(), rootVisualElement.Q<Label>(TestId).PlainText());
        }

        [Test]
        public void 隠すとtestidの要素が木から無くなる()
        {
            // Web は継承しないとき null を返して testid の要素ごと描かない。出していたバッジを隠しても同じになる
            // The Web returns null and renders no testid element without inheritance; hiding a shown badge matches that
            var badge = new LevelInheritanceBadge(TestId);
            Assert.IsTrue(LevelInheritanceBadgeText.TryBuild(RecipeLevelInheritance.InheritUpToUnlocked(0, Ids(11, 12), 0, Ids(21, 22), 1), out var shown));
            badge.Show(shown);
            rootVisualElement.Add(badge);
            simulate.FrameUpdate();

            Assert.IsFalse(LevelInheritanceBadgeText.TryBuild(RecipeLevelInheritance.None, out _));
            badge.Hide();
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, badge.resolvedStyle.display);
            Assert.IsNull(rootVisualElement.Q(TestId), "no element carries the testid after Hide");
            Assert.IsNull(badge.Q<Label>());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 文はMantineのxsとdimmedでtestidは文の要素が持つ()
        {
            var badge = new LevelInheritanceBadge(TestId);
            Assert.IsTrue(LevelInheritanceBadgeText.TryBuild(RecipeLevelInheritance.InheritUpToUnlocked(0, Ids(11), 0, Ids(21), 1), out var text));
            badge.Show(text);
            rootVisualElement.Add(badge);
            simulate.FrameUpdate();

            // testid は文のラベルが持ち（Web の Text の data-testid）、根は名前を持たない
            // The text label carries the testid (the Web Text data-testid); the root has no name
            Assert.IsTrue(string.IsNullOrEmpty(badge.name));
            var label = badge.Q<Label>(TestId);
            Assert.AreEqual(ResolvePixels("--mantine-font-size-xs"), label.resolvedStyle.fontSize, 0.01f);
            AssertSameColor(ResolveColor("--mantine-color-dimmed"), label.resolvedStyle.color, "dimmed");
        }
    }
}

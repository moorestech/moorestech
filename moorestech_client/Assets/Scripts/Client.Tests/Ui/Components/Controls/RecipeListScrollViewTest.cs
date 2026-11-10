using System.Collections.Generic;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ThemeTokenFile;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// RecipeListScrollView の名前・行の逃げ余白・バーの幅・先頭の内容・行の間隔（L1）
    /// RecipeListScrollView names, row bleed padding, bar width, leading content and row gap (L1)
    /// </summary>
    public class RecipeListScrollViewTest : UiTestHost
    {
        private const string ListTestId = "recipe-list-test";
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/Controls/ControlKnobOverrides.uss";

        [Test]
        public void 列の名前はlistTestidでスクロールはその後ろにscrollを付ける()
        {
            var view = Mount(rootVisualElement, 2);
            Assert.IsNotNull(view.Q<GameScrollView>($"{ListTestId}-scroll"));
            Assert.IsInstanceOf<UiStack>(view.Q(ListTestId));
            Assert.AreEqual(2, view.Q(ListTestId).childCount);
        }

        [Test]
        public void 列の余白は四方bleedで右だけbleedとreserveの和()
        {
            // Web の .list の padding: bleed と padding-right: calc(bleed + reserve)
            // Web .list padding: bleed and padding-right: calc(bleed + reserve)
            var list = Mount(rootVisualElement, 2).Q(ListTestId);
            var bleed = ResolvePixels("--recipe-entry-bleed");
            Assert.AreEqual(bleed, list.resolvedStyle.paddingTop, 0.01f);
            Assert.AreEqual(bleed, list.resolvedStyle.paddingBottom, 0.01f);
            Assert.AreEqual(bleed, list.resolvedStyle.paddingLeft, 0.01f);
            Assert.AreEqual(bleed + ResolvePixels("--recipe-list-scrollbar-reserve"), list.resolvedStyle.paddingRight, 0.01f);
        }

        [Test]
        public void 溢れたときのバーの幅はreserveで先頭の内容は列の前に入り外せる()
        {
            var view = Mount(rootVisualElement, 6);
            var scroll = view.Q<GameScrollView>($"{ListTestId}-scroll");
            // 溢れていることを先に確かめる（溢れなければ GameScrollView がバーを display: none にする）
            // Check the overflow first (without it GameScrollView hides the bar with display: none)
            Assert.Greater(scroll.contentContainer.layout.height, scroll.contentViewport.layout.height, "six rows overflow the 100px host");
            Assert.AreEqual(DisplayStyle.Flex, scroll.verticalScroller.resolvedStyle.display, "bar shown on overflow");
            Assert.AreEqual(ResolvePixels("--recipe-list-scrollbar-reserve"), scroll.verticalScroller.worldBound.width, 0.01f);

            // 先頭の内容は同じスクロールの中で列より上に並ぶ
            // Leading content sits above the list inside the same scroller
            var leading = new VisualElement { name = "leading-probe" };
            leading.style.height = 20f;
            view.SetLeadingContent(leading);
            simulate.FrameUpdate();
            Assert.AreEqual(leading.worldBound.yMax, view.Q(ListTestId).worldBound.yMin, 0.5f);
            view.ClearLeadingContent();
            simulate.FrameUpdate();
            Assert.IsNull(view.Q("leading-probe"));
        }

        [Test]
        public void 行の間隔は既定で10pxで機械のレシピ選択の上書きでは8px()
        {
            var plain = Mount(rootVisualElement, 2);
            Assert.AreEqual(ResolvePixels("--recipe-entry-gap"), Gap(plain), 0.5f);
            plain.parent.RemoveFromHierarchy();

            var machine = Mount(Probe("control-recipe-list-machine-probe"), 2);
            Assert.AreEqual(ResolvePixels("--machine-recipe-row-gap"), Gap(machine), 0.5f);
        }

        [Test]
        public void bleedを上書きすると四方と右の和が追う()
        {
            var list = Mount(Probe("control-recipe-list-bleed-probe"), 2).Q(ListTestId);
            Assert.AreEqual(7f, list.resolvedStyle.paddingLeft, 0.01f);
            Assert.AreEqual(7f + ResolvePixels("--recipe-list-scrollbar-reserve"), list.resolvedStyle.paddingRight, 0.01f);
        }

        private RecipeListScrollView Mount(VisualElement parent, int entryCount)
        {
            // 高さ 100 の器に載せ、高さ 40 の行を並べる（ビューは flex-grow: 1 で器いっぱいになり、6 行で溢れる。前例 GameScrollViewTest の高さを決めた器）
            // Mount inside a 100px host with 40px rows (the view grows to the host via flex-grow: 1 and six rows overflow; precedent GameScrollViewTest's sized host)
            var host = new VisualElement();
            host.style.height = 100f;
            parent.Add(host);
            var view = new RecipeListScrollView(ListTestId);
            var entries = new List<VisualElement>();
            for (var i = 0; i < entryCount; i++)
            {
                var entry = new VisualElement { name = $"entry-{i}" };
                entry.style.height = 40f;
                entries.Add(entry);
            }
            view.SetEntries(entries);
            host.Add(view);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return view;
        }

        private VisualElement Probe(string className)
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(sheet, ProbePath);
            var holder = new VisualElement();
            holder.styleSheets.Add(sheet);
            holder.AddToClassList(className);
            rootVisualElement.Add(holder);
            return holder;
        }

        private static float Gap(RecipeListScrollView view)
        {
            return view.Q("entry-1").worldBound.yMin - view.Q("entry-0").worldBound.yMax;
        }
    }
}

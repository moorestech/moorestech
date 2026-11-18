using Client.Tests.Ui.Components;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Components.Slots;
using Client.Ui.Screens.Recipe.Parts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Recipe.ItemList
{
    /// <summary>
    /// 一覧のスクロール領域の寸法・バーの出る段数・最終段の逃げ・つまみを画面の USS の下で確かめる（L1）
    /// Verifies the list scroll area's geometry, the row count that shows the bar, the last-row clearance and the thumb under the screen USS (L1)
    /// </summary>
    public class ItemListScrollGeometryTest : ItemListTestHost
    {
        /// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">スクロール領域はパネル本文いっぱいで、既定件数では溢れずバーも出ない</web-test>
        [Test]
        public void 視口は本文いっぱいで既定件数では溢れずバーも出ない()
        {
            var view = MountList(ListOf(30));
            var scroll = view.Q<GameScrollView>("item-list-scroll");
            var viewport = scroll.contentViewport.worldBound;
            var body = view.Q<GamePanel>("item-list-panel").contentContainer.worldBound;
            var grid = view.Q<SlotGrid>("item-list-grid").worldBound;

            // Web: viewportBottom − bodyBottom ≈ 12（clip-inset）
            // Web: viewportBottom - bodyBottom is about 12 (clip-inset)
            Assert.AreEqual(12f, viewport.yMax - body.yMax, 1f);
            Assert.Greater(viewport.yMax - grid.yMax, 100f);
            Assert.AreEqual(DisplayStyle.None, scroll.verticalScroller.resolvedStyle.display);
        }

        /// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">パネルを超える件数で初めて縦バーが出る。横は常に出ない</web-test>
        [Test]
        public void パネルを超える件数で初めて縦バーが出て横は出ない()
        {
            var seven = MountList(ListOf(42)).Q<GameScrollView>("item-list-scroll");
            Assert.AreEqual(DisplayStyle.None, seven.verticalScroller.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, seven.horizontalScroller.resolvedStyle.display);

            rootVisualElement.Clear();
            var eight = MountList(ListOf(48)).Q<GameScrollView>("item-list-scroll");
            Assert.AreEqual(DisplayStyle.Flex, eight.verticalScroller.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, eight.horizontalScroller.resolvedStyle.display);
        }

        /// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">7段は溢れず、最下段までスクロールしても最終段に逃げが残る</web-test>
        [Test]
        public void 七段は溢れず最下段までスクロールしても最終段の下に逃げが残る()
        {
            var seven = MountList(ListOf(42)).Q<GameScrollView>("item-list-scroll");
            Assert.LessOrEqual(seven.GetContentHeight(), seven.GetViewportHeight());

            // 60 件（10 段）を最下段まで送っても、最終段の下に逃げ 12 が残る
            // Scrolling 60 items (ten rows) to the bottom still leaves the 12 clearance under the last row
            rootVisualElement.Clear();
            var view = MountList(ListOf(60));
            var scroll = view.Q<GameScrollView>("item-list-scroll");
            scroll.SetScrollOffset(scroll.verticalScroller.highValue);
            simulate.FrameUpdate();
            Assert.Greater(scroll.scrollOffset.y, 0f);
            var lastCell = view.Q<ItemListCellView>("item-list-cell-60").worldBound;
            Assert.GreaterOrEqual(scroll.contentViewport.worldBound.yMax - lastCell.yMax, 12f);
        }

        [Test]
        public void 一覧のつまみ本体は幅4でホバーしても白のまま()
        {
            var scroll = MountList(ListOf(60)).Q<GameScrollView>("item-list-scroll");
            var dragger = scroll.verticalScroller.Q(className: "unity-base-slider__dragger");
            var white88 = ComponentStyleAssert.Rgba(255, 255, 255, 0.88f);
            Assert.AreEqual(4f, dragger.resolvedStyle.width, 0.01f);
            Assert.AreEqual(0f, scroll.verticalScroller.resolvedStyle.paddingLeft, 0.001f);
            ComponentStyleAssert.AssertColor(ComponentStyleAssert.Rgba(0, 0, 0, 0f), scroll.verticalScroller.resolvedStyle.backgroundColor, "track");
            ComponentStyleAssert.AssertColor(white88, dragger.resolvedStyle.backgroundColor, "thumb");

            // :hover にしても GameScrollView の既定のホバー灰に変わらない
            // Hovering does not switch to GameScrollView's default grey hover
            simulate.MouseMove(dragger.worldBound.center);
            simulate.FrameUpdate();
            ComponentStyleAssert.AssertColor(white88, dragger.resolvedStyle.backgroundColor, "hovered thumb");

            // 縦バーは上に逃げ 12、下に逃げ 12 と Web の corner 4 を空ける（参照のつまみ 277px）
            // The bar leaves 12 above and 12 plus the Web corner 4 below (reference thumb 277px)
            Assert.AreEqual(12f, scroll.verticalScroller.worldBound.y - scroll.worldBound.y, 0.5f);
            Assert.AreEqual(16f, scroll.worldBound.yMax - scroll.verticalScroller.worldBound.yMax, 0.5f);
        }
    }
}

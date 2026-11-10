using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.Screens.Recipe.ItemList;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Screens.Recipe;
using Client.Ui.Screens.Tutorial.Anchor;
using Client.Ui.State.Tutorial.Logic;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.Recipe.RecipeScreenFixtures;

namespace Client.Tests.Ui.Screens.Recipe.Overlay
{
    /// <summary>
    /// 一覧とレシピ列のアンカーを S2-tutorial の測定で測り、ラベル・枠の出し入れとクリップの逃げを Web の判定で確かめる（L1）
    /// Measures list and recipe-list anchors with the S2-tutorial measure and checks label/ring visibility and clip clearance with the Web rules (L1)
    /// </summary>
    public class RecipeTutorialClipTest : ItemListTestHost
    {
        private static float Padding => ThemeTokenFile.ResolvePixels("--tutorial-anchor-padding");
        private static float Glow => ThemeTokenFile.ResolvePixels("--tutorial-highlight-glow");

        /// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">溢れていない一覧の最上段でもチュートリアルのラベルが出る</web-test>
        [Test]
        public void 溢れていない一覧の最上段でもラベルが出る()
        {
            MountList(ListOf(30));
            var ready = MeasureReady(AnchorIds.RecipeItem(1));
            Assert.IsTrue(TutorialHighlightLabelPlacement.ShowsLabel(ready.Rect, ready.Clip));
            Assert.IsTrue(TutorialHighlightClip.TryComputeInset(ready.Rect.Expand(Padding), ready.Clip, Glow, out _));
        }

        /// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">アンカーがスクロールで隠れ始めたらラベルを引っ込め、完全に隠れたら枠も消す</web-test>
        [Test]
        public void 隠れ始めたらラベルを引っ込め完全に隠れたら枠も消える()
        {
            var scroll = MountList(ListOf(MaxItemId)).Q<GameScrollView>("item-list-scroll");

            // 溢れていてもスクロール前の最上段はラベルが出る
            // Even when overflowing, the top row shows the label before scrolling
            var top = MeasureReady(AnchorIds.RecipeItem(1));
            Assert.IsTrue(TutorialHighlightLabelPlacement.ShowsLabel(top.Rect, top.Clip));

            // 一部が隠れたらラベルは引っ込み、枠は削られて残る
            // Once partly hidden the label retreats while the ring stays clipped
            ScrollTo(scroll, 20f);
            var partly = MeasureReady(AnchorIds.RecipeItem(1));
            Assert.IsFalse(TutorialHighlightLabelPlacement.ShowsLabel(partly.Rect, partly.Clip));
            Assert.IsTrue(TutorialHighlightClip.TryComputeInset(partly.Rect.Expand(Padding), partly.Clip, Glow, out _));

            // 完全に隠れたら枠も描かない（ADR 0024）。一覧はパネルの上端に近く、セルが画面の外へ出て Hidden になる場合も枠は出ない
            // Fully hidden, the ring is not drawn (ADR 0024); the list sits near the panel top, so the cell may leave the screen as Hidden, which also draws no ring
            ScrollTo(scroll, 300f);
            var hidden = TutorialAnchorMeasure.Measure(TutorialAnchorRegistry.Resolve(rootVisualElement, AnchorIds.RecipeItem(1)), rootVisualElement);
            var ringDrawn = hidden is TutorialAnchorState.Ready ready && TutorialHighlightClip.TryComputeInset(ready.Rect.Expand(Padding), ready.Clip, Glow, out _);
            Assert.IsFalse(ringDrawn, hidden.Reason.ToString());
        }

        /// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">クリップ境界はグリッドからハイライトの逃げぶん離れ、枠が削られない</web-test>
        [Test]
        public void 一覧のクリップ境界はグリッドから逃げぶん離れる()
        {
            var view = MountList(ListOf(30));
            var grid = view.Q<SlotGrid>("item-list-grid").worldBound;
            var clip = view.Q<GameScrollView>("item-list-scroll").contentViewport.worldBound;
            var required = Padding + Glow;
            Assert.Greater(required, 0f);
            Assert.GreaterOrEqual(grid.yMin - clip.yMin, required, "top");
            Assert.GreaterOrEqual(grid.xMin - clip.xMin, required, "left");
            Assert.GreaterOrEqual(clip.xMax - grid.xMax, required, "right");

            // 枠は四辺とも削られない（inset は −glow＝グロー分だけ外へ出る。Web の inset(-4px)）
            // No side of the ring is shaved (inset is -glow, extending by the glow; the Web inset(-4px))
            var ready = MeasureReady(AnchorIds.RecipeItem(1));
            Assert.IsTrue(TutorialHighlightClip.TryComputeInset(ready.Rect.Expand(Padding), ready.Clip, Glow, out var inset));
            AssertUnshaved(inset);
        }

        /// <web-test file="e2e/tests/recipe/recipeListScroll.spec.ts">クリップ境界はエントリからハイライトの逃げぶん離れ、最下段までスクロールしても保たれる</web-test>
        [Test]
        public void レシピ列のクリップ境界は最下段までスクロールしても逃げぶん離れる()
        {
            var entries = ManyCrafts(8);
            var view = Mount(new RecipeViewerView(Context(Toaster, new RecordingRecipeCommands(), Selection)));
            view.Render(Selected(7, entries));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var scroll = view.Q<GameScrollView>("recipe-entry-list-scroll");
            Assert.AreEqual(DisplayStyle.Flex, scroll.verticalScroller.resolvedStyle.display);
            var required = Padding + Glow;

            // 先頭のエントリは上・左・右に逃げがあり、先頭のボタンの枠は削られない
            // The first entry keeps clearance above, left and right, and the first button's ring is not shaved
            var first = view.Q($"craft-recipe-entry-{entries[0].RecipeGuid}").worldBound;
            var clip = scroll.contentViewport.worldBound;
            Assert.GreaterOrEqual(first.yMin - clip.yMin, required, "top");
            Assert.GreaterOrEqual(first.xMin - clip.xMin, required, "left");
            Assert.GreaterOrEqual(clip.xMax - first.xMax, required, "right");
            var button = MeasureReady(AnchorIds.RecipeCraftButton);
            Assert.IsTrue(TutorialHighlightClip.TryComputeInset(button.Rect.Expand(Padding), button.Clip, Glow, out var inset));
            AssertUnshaved(inset);

            // 最下段まで送っても末尾のエントリの下に逃げが残る（D-47 の内側余白）
            // Scrolled to the end, the last entry keeps its clearance below (the D-47 content padding)
            ScrollTo(scroll, scroll.verticalScroller.highValue);
            Assert.Greater(scroll.scrollOffset.y, 0f);
            var last = view.Q($"craft-recipe-entry-{entries[7].RecipeGuid}").worldBound;
            Assert.GreaterOrEqual(scroll.contentViewport.worldBound.yMax - last.yMax, required, "bottom");
        }

        private TutorialAnchorState.Ready MeasureReady(string anchorId)
        {
            var state = TutorialAnchorMeasure.Measure(TutorialAnchorRegistry.Resolve(rootVisualElement, anchorId), rootVisualElement);
            Assert.IsInstanceOf<TutorialAnchorState.Ready>(state, $"{anchorId}: {state.Reason}");
            return (TutorialAnchorState.Ready)state;
        }

        private void ScrollTo(GameScrollView scroll, float y)
        {
            scroll.SetScrollOffset(y);
            simulate.FrameUpdate();
        }

        private static void AssertUnshaved(TutorialClipInset inset)
        {
            Assert.AreEqual(-Glow, inset.Top, 0.01f, "top");
            Assert.AreEqual(-Glow, inset.Right, 0.01f, "right");
            Assert.AreEqual(-Glow, inset.Bottom, 0.01f, "bottom");
            Assert.AreEqual(-Glow, inset.Left, 0.01f, "left");
        }
    }
}

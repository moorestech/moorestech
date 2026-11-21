using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.Machine.RecipeSelection;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// ウィンドウが低いときの清浄室の配置（舞台 1280×720 と縮めた 600。部屋欄は台数に関係なく最大3行。ADR 0105 §5）
    /// Clean-room layout with low windows (the 1280x720 stage and a shrunk 600; the room section is at most three lines whatever the count; ADR 0105 §5)
    /// </summary>
    public class CleanRoomLayoutTest : CleanRoomLayoutTestHost
    {
        private const string NaturalScrollName = "block-inventory-natural-scroll";

        [TestCase(600f)]
        [TestCase(720f)]
        public void 清浄機パネルは部屋欄の最後の行とフィルタ枠へ届き枠が安全帯へ潜らない(float stageHeight)
        {
            var shell = OpenAirFilterPanel();
            SetStageHeight(shell, stageHeight);
            ExpectPanelAboveSafeBand(shell);
            var grid = shell.View.Q("clean-room-filter-grid");
            var scroll = grid.GetFirstAncestorOfType<GameScrollView>();
            ExpectReachableInScrollViewport(scroll, shell.View.Q("clean-room-queried-air-filter"));
            ExpectReachableInScrollViewport(scroll, grid);
            ExpectPanelAboveSafeBand(shell);

            // 部屋欄とフィルタ枠は入れ子なしの同じ視口
            // Room rows and the filter grid share one viewport with no nested vertical scroller
            Assert.IsNotNull(scroll.contentContainer.Q("clean-room-section"));
            Assert.IsNull(scroll.GetFirstAncestorOfType<GameScrollView>());
        }

        [Test]
        public void 清浄機パネルは自然高のままスクロールしない()
        {
            var shell = OpenAirFilterPanel();
            var scroll = shell.View.Q("clean-room-filter-grid").GetFirstAncestorOfType<GameScrollView>();
            Assert.AreEqual(NaturalScrollName, scroll.name);
            Assert.LessOrEqual(scroll.GetContentHeight(), scroll.GetViewportHeight() + 0.01f);
            ExpectPanelAboveSafeBand(shell);
        }

        [Test]
        public void 部屋の中の室内機械で液体行が電力行と状態行に重ならない()
        {
            var shell = OpenCleanRoomMachine(true);
            Assert.IsNotNull(shell.View.Q("clean-room-headline"));

            // 本文側は視口で切った可視矩形で比べる
            // Compare the body side by its rect clipped to the scroll viewport
            var fluids = shell.View.Q("machine-fluid-slots");
            var viewport = fluids.GetFirstAncestorOfType<GameScrollView>().contentViewport.worldBound;
            var visibleFluids = Intersect(fluids.worldBound, viewport);
            foreach (var name in new[] { "machine-power-rate", "machine-state-label" })
            {
                var overlap = Intersect(visibleFluids, shell.View.Q(name).worldBound);
                Assert.IsFalse(0 < overlap.width && 0 < overlap.height, $"{name} overlap {overlap.width}x{overlap.height}");
            }
            ExpectFooterClearOfBody(shell);
        }

        [TestCase(600f)]
        [TestCase(720f)]
        public void 部屋の中の室内機械は部屋欄ごと本文がレシピとスロットと液体に届きフッタが読める(float stageHeight)
        {
            var shell = OpenCleanRoomMachine(true);
            SetStageHeight(shell, stageHeight);
            ExpectFooterClearOfBody(shell);
            ExpectReachable(shell, "clean-room-headline");
            ExpectInventoryBodyReachable(shell);
        }

        [Test]
        public void 部屋の中の室内機械のレシピ選択でも部屋欄と同じ視口で最後のレシピ行へ届く()
        {
            var shell = OpenCleanRoomMachine(true);
            ExpectReachable(shell, "machine-selected-recipe");
            simulate.Click(shell.View.Q("machine-selected-recipe").worldBound.center);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            ExpectFooterReadable(shell);
            var rows = shell.View.Q(MachineRecipeSelectionList.ListTestid).Query<MachineRecipeSelectionRowView>().ToList();
            Assert.Greater(rows.Count, 0);
            var scroll = rows[0].GetFirstAncestorOfType<GameScrollView>();
            ExpectReachableInScrollViewport(scroll, rows[0]);
            ExpectReachableInScrollViewport(scroll, rows[rows.Count - 1]);
            ExpectSharedBodyViewport(shell, MachineRecipeSelectionList.ListTestid);
        }

        [Test]
        public void 部屋の中の室内機械で改善のボタンへ届き押すと改善方法を開く()
        {
            var shell = OpenCleanRoomMachine(true);
            var improvements = new List<CleanRoomOddsImprovement> { CleanRoomOddsImprovement.AddQualityModule(1, new[] { 0.1 }, -0.1) };
            var yield = SectionContent<CleanRoomYieldOdds>.Present(CleanRoomYieldOdds.Dicing(300, new[] { 0.4 }, 0.6, improvements));
            ContextModel.Set(new BlockContextViewModel(MachineRoom().CleanRoom, yield, SectionContent<MachineInputStall>.Absent));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            // 部屋欄・確率の欄と同じ視口の中でボタンへ届き、押下は開く口へ届く
            // The button is reachable in the viewport shared with the room and odds sections, and its press reaches the open entry
            ExpectReachable(shell, "clean-room-yield-improvement-open");
            simulate.Click(shell.View.Q("clean-room-yield-improvement-open").worldBound.center);
            Assert.AreEqual(1, ImprovementCommands.OpenCount);
            ExpectFooterReadable(shell);
        }

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomManyFilters.spec.ts">部屋の無い大型機械は部屋欄なしで本文に届き、フッタが読める</web-test>
        [Test]
        public void 部屋の無い大型機械は部屋欄なしで本文に届きフッタが読める()
        {
            var shell = OpenCleanRoomMachine(false);
            Assert.IsNull(shell.View.Q("clean-room-section"));
            foreach (var name in new[] { "machine-selected-recipe", "machine-input-slots", "machine-fluid-slots" }) ExpectReachable(shell, name);
            ExpectFooterReadable(shell);
        }

        // Web expectPanelAboveHotbar の写し
        // Web expectPanelAboveHotbar: the panel bottom stays above the safe band
        private static void ExpectPanelAboveSafeBand(BlockInventoryTestShell shell)
        {
            Assert.LessOrEqual(shell.View.worldBound.yMax, shell.Stage.worldBound.yMax - BlockInventoryTestShell.MenuLowerSafeArea + 0.5f);
        }

        // Web expectFooterClearOfBody
        // Web expectFooterClearOfBody: the footer stays below the body viewport
        private static void ExpectFooterClearOfBody(BlockInventoryTestShell shell)
        {
            var panel = shell.View.worldBound;
            var viewport = shell.View.Q("machine-inventory-body").GetFirstAncestorOfType<GameScrollView>().contentViewport.worldBound;
            foreach (var name in new[] { "machine-state-label", "machine-power-rate" })
            {
                var footer = shell.View.Q(name).worldBound;
                Assert.LessOrEqual(footer.yMax, panel.yMax + 0.5f, $"{name} inside panel");
                Assert.LessOrEqual(viewport.yMax, footer.yMin + 0.5f, $"viewport above {name}");
            }
        }

        private static Rect Intersect(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            return new Rect(xMin, yMin, Mathf.Min(a.xMax, b.xMax) - xMin, Mathf.Min(a.yMax, b.yMax) - yMin);
        }
    }
}

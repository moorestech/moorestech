using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.Machine.RecipeSelection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 部屋欄が長い・ウィンドウが低いときの清浄室の配置（Web cleanRoom/*.spec.ts の写し。舞台 1280×720 と縮めた 600。1080 の件は録画で見る D-15）
    /// Clean-room layout with long room sections and low windows (ports of Web cleanRoom/*.spec.ts on the 1280x720 stage and a shrunk 600; 1080 goes to recordings, D-15)
    /// </summary>
    public class CleanRoomLayoutTest : CleanRoomLayoutTestHost
    {
        private const string NaturalScrollName = "block-inventory-natural-scroll";

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomAirFilterPanel.spec.ts">清浄機パネル(${room.fixture})・高さ${windowHeight}で部屋欄の末尾とフィルタ枠へ届き、枠がホットバーへ潜らない</web-test>
        [TestCase(2, 600f)]
        [TestCase(2, 720f)]
        [TestCase(8, 600f)]
        [TestCase(8, 720f)]
        [TestCase(14, 600f)]
        [TestCase(14, 720f)]
        [TestCase(20, 600f)]
        [TestCase(20, 720f)]
        [TestCase(30, 600f)]
        [TestCase(30, 720f)]
        public void 清浄機パネルは部屋欄の末尾とフィルタ枠へ届き枠が安全帯へ潜らない(int filterCount, float stageHeight)
        {
            // 30 台の未接続（cleanRoomAirFilter30FiltersUnconnected）は電力網の行を UITK が持たないので 30 台と同じ形になる
            // The unconnected 30-unit fixture has the same shape as 30 here, since UITK has no electric-network row
            var shell = OpenAirFilterPanel(filterCount);
            SetStageHeight(shell, stageHeight);
            ExpectPanelAboveSafeBand(shell);
            var grid = shell.View.Q("clean-room-filter-grid");
            var scroll = grid.GetFirstAncestorOfType<GameScrollView>();
            ExpectReachableInScrollViewport(scroll, shell.View.Q($"clean-room-air-filter-{filterCount - 1}"));
            ExpectReachableInScrollViewport(scroll, grid);
            ExpectPanelAboveSafeBand(shell);

            // 部屋欄とフィルタ枠は入れ子なしの同じ視口
            // Room rows and the filter grid share one viewport with no nested vertical scroller
            Assert.IsNotNull(scroll.contentContainer.Q("clean-room-section"));
            Assert.IsNull(scroll.GetFirstAncestorOfType<GameScrollView>());
        }

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomAirFilterPanel.spec.ts">清浄機2台・高さ720は自然高のままスクロールしない</web-test>
        [Test]
        public void 清浄機2台は自然高のままスクロールしない()
        {
            var shell = OpenAirFilterPanel(2);
            var scroll = shell.View.Q("clean-room-filter-grid").GetFirstAncestorOfType<GameScrollView>();
            Assert.AreEqual(NaturalScrollName, scroll.name);
            Assert.LessOrEqual(scroll.GetContentHeight(), scroll.GetViewportHeight() + 0.01f);
            ExpectPanelAboveSafeBand(shell);
        }

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomMachineLayout.spec.ts">清浄機2台の室内機械で液体行が電力行・状態行に重ならない</web-test>
        [Test]
        public void 清浄機2台の室内機械で液体行が電力行と状態行に重ならない()
        {
            var shell = OpenCleanRoomMachine(2);
            Assert.IsNotNull(shell.View.Q("clean-room-air-filter-1"));

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

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomMachineLayout.spec.ts">低いウィンドウで清浄機が多い部屋でもフッタは見え、本文はスクロールで液体行へ届く</web-test>
        [Test]
        public void 低いウィンドウで清浄機が多い部屋でもフッタは見え本文はスクロールで液体行へ届く()
        {
            // Web と同じ高さ 600 に縮める
            // Shrink the stage to Web's 600 height
            var shell = OpenCleanRoomMachine(8);
            SetStageHeight(shell, 600f);
            Assert.IsNotNull(shell.View.Q("clean-room-air-filter-7"));
            ExpectFooterClearOfBody(shell);

            // 溢れた本文はスクロールで液体行へ届く
            // The overflowing body scrolls within its container and reaches the fluid row
            var scroll = shell.View.Q("machine-inventory-body").GetFirstAncestorOfType<GameScrollView>();
            Assert.Greater(scroll.GetContentHeight() - scroll.GetViewportHeight(), 0f);
            ExpectReachable(shell, "machine-fluid-slots");
        }

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomManyFilters.spec.ts">清浄機${room.filters}台・高さ${windowHeight}でも部屋欄ごと本文がスクロールし、レシピ・スロット・液体に届きフッタが読める</web-test>
        [TestCase(16, 600f)]
        [TestCase(16, 720f)]
        [TestCase(20, 600f)]
        [TestCase(20, 720f)]
        public void 清浄機が多くても部屋欄ごと本文がスクロールしレシピとスロットと液体に届きフッタが読める(int filterCount, float stageHeight)
        {
            var shell = OpenCleanRoomMachine(filterCount);
            SetStageHeight(shell, stageHeight);
            ExpectReachable(shell, $"clean-room-air-filter-{filterCount - 1}");
            ExpectInventoryBodyReachable(shell);
        }

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomManyFilters.spec.ts">清浄機20台・高さ${windowHeight}のレシピ選択でも部屋欄と同じ視口で最後のレシピ行へ届く</web-test>
        [Test]
        public void 清浄機20台のレシピ選択でも部屋欄と同じ視口で最後のレシピ行へ届く()
        {
            // 部屋欄が長いのでヘッダを視口へ寄せてから押す
            // The room section is long, so scroll the header into the viewport before pressing it
            var shell = OpenCleanRoomMachine(20);
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

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomManyFilters.spec.ts">清浄機${room.filters}台・高さ720の境目でも本文とフッタが使える</web-test>
        [TestCase(0)]
        [TestCase(2)]
        [TestCase(8)]
        public void 清浄機の台数の境目でも本文とフッタが使える(int filterCount)
        {
            ExpectInventoryBodyReachable(OpenCleanRoomMachine(filterCount));
        }

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomManyFilters.spec.ts">部屋の無い大型機械は部屋欄なしで本文に届き、フッタが読める</web-test>
        [Test]
        public void 部屋の無い大型機械は部屋欄なしで本文に届きフッタが読める()
        {
            var shell = OpenCleanRoomMachine(-1);
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

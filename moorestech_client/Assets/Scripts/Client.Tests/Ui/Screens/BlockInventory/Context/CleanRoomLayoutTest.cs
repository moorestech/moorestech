using Client.Ui.Components.Controls;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 部屋欄が長い・ウィンドウが低いときの清浄室の配置（Web cleanRoom/*.spec.ts の写し。舞台 1280×720。600・1080 の件は録画で見る D-15）
    /// Clean-room layout with long room sections and low windows (ports of Web cleanRoom/*.spec.ts on the 1280x720 stage; 600/1080 go to recordings, D-15)
    /// </summary>
    public class CleanRoomLayoutTest : CleanRoomLayoutTestHost
    {
        private const string NaturalScrollName = "block-inventory-natural-scroll";

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomAirFilterPanel.spec.ts">清浄機パネル(${room.fixture})・高さ${windowHeight}で部屋欄の末尾とフィルタ枠へ届き、枠がホットバーへ潜らない</web-test>
        [TestCase(2)]
        [TestCase(8)]
        [TestCase(14)]
        [TestCase(20)]
        [TestCase(30)]
        public void 清浄機パネルは部屋欄の末尾とフィルタ枠へ届き枠が安全帯へ潜らない(int filterCount)
        {
            // 30 台の未接続（cleanRoomAirFilter30FiltersUnconnected）は電力網の行を UITK が持たないので 30 台と同じ形になる
            // The unconnected 30-unit fixture has the same shape as 30 here, since UITK has no electric-network row
            var shell = OpenAirFilterPanel(filterCount);
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
            Assert.IsTrue(shell.View.ClassListContains(Client.Ui.Screens.BlockInventory.BlockInventoryView.LargeClassName));

            // 本文側は視口で切った可視矩形で比べる
            // Compare the body side by its rect clipped to the scroll viewport
            var fluids = shell.View.Q("machine-fluid-slots");
            var viewport = fluids.GetFirstAncestorOfType<GameScrollView>().contentViewport.worldBound;
            var visibleFluids = Intersect(fluids.worldBound, viewport);
            foreach (var name in new[] { "machine-power-rate", "machine-state-label" })
            {
                var overlap = Intersect(visibleFluids, shell.View.Q(name).worldBound);
                Assert.IsFalse(overlap.width > 0 && overlap.height > 0, $"{name} overlap {overlap.width}x{overlap.height}");
            }
            ExpectFooterClearOfBody(shell);
        }

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomMachineLayout.spec.ts">低いウィンドウで清浄機が多い部屋でもフッタは見え、本文はスクロールで液体行へ届く</web-test>
        [Test]
        public void 低いウィンドウで清浄機が多い部屋でもフッタは見え本文はスクロールで液体行へ届く()
        {
            // Web と同じ高さ 600 へ舞台を縮める（縮めるだけなら 720 の土台に収まる。BlockInventoryPanelBandTest の前例）
            // Shrink the stage to the Web's 600 height (shrinking fits the 720 host; BlockInventoryPanelBandTest precedent)
            var shell = OpenCleanRoomMachine(8);
            shell.Stage.style.height = 600f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsNotNull(shell.View.Q("clean-room-air-filter-7"));
            ExpectFooterClearOfBody(shell);

            // 溢れた本文はスクロールで液体行へ届く
            // The overflowing body scrolls within its container and reaches the fluid row
            var scroll = shell.View.Q("machine-inventory-body").GetFirstAncestorOfType<GameScrollView>();
            Assert.Greater(scroll.GetContentHeight() - scroll.GetViewportHeight(), 0f);
            ExpectReachable(shell, "machine-fluid-slots");
        }

        /// <web-test file="e2e/tests/block/cleanRoom/cleanRoomManyFilters.spec.ts">清浄機${room.filters}台・高さ${windowHeight}でも部屋欄ごと本文がスクロールし、レシピ・スロット・液体に届きフッタが読める</web-test>
        [TestCase(16)]
        [TestCase(20)]
        public void 清浄機が多くても部屋欄ごと本文がスクロールしレシピとスロットと液体に届きフッタが読める(int filterCount)
        {
            var shell = OpenCleanRoomMachine(filterCount);
            ExpectReachable(shell, $"clean-room-air-filter-{filterCount - 1}");
            ExpectInventoryBodyReachable(shell);
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

        // Web expectPanelAboveHotbar: パネルの下端は舞台の下の安全帯より上
        // Web expectPanelAboveHotbar: the panel bottom stays above the stage's lower safe band
        private static void ExpectPanelAboveSafeBand(BlockInventoryTestShell shell)
        {
            Assert.LessOrEqual(shell.View.worldBound.yMax, shell.Stage.worldBound.yMax - MenuLowerSafeArea + 0.5f);
        }

        // Web expectFooterClearOfBody: フッタはパネル内で、本文の視口はその上で終わる
        // Web expectFooterClearOfBody: the footer stays inside the panel and the body viewport ends above it
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

using System.Text.RegularExpressions;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.ModeHud;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Items;
using Client.Ui.State.Toast;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 外殻の開閉と閉じる操作（L1。Web e2e の block inventory の写し）
    /// Shell open/close and the close action (L1; port of the Web e2e block inventory)
    /// </summary>
    public class BlockInventoryPanelFlowTest : BlockInventoryScreenTestHost
    {
        private PlacementNameTestDictionary _dictionary;

        [SetUp]
        public void SetUpMasterNames()
        {
            // マスタの原文（ブロック名）を辞書へ重ねる（本番の起動と同じ合成。行は足さない）
            // Overlay the master source texts (block names) as the production boot does; no extra rows
            _dictionary = new PlacementNameTestDictionary();
            _dictionary.Merge(string.Empty);
        }

        [TearDown]
        public void TearDownMasterNames()
        {
            _dictionary.Restore();
        }

        /// <web-test file="e2e/tests/block/blockInventory.spec.ts">chest の block inventory パネルが描画される</web-test>
        [Test]
        public void チェストを開くと題と項目グリッドが出る()
        {
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var shell = BlockInventoryTestShell.Create(this, state);
            state.Value = BlockInventoryTestPanels.ChestWithSlots(new SlotItem(new ItemId(1), 1));
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.Flex, shell.View.resolvedStyle.display);
            Assert.IsNotNull(shell.View.Q("block-inventory-panel"));
            Assert.IsNotNull(shell.View.Q("chest-grid"));
            Assert.IsNotNull(shell.View.Q("chest-grid-slot-0"));
            Assert.AreEqual(BlockInventoryTestPanels.ChestName, shell.TitleText());
        }

        /// <web-test file="e2e/tests/block/blockInventory.spec.ts">ブロックを閉じると panel が消える</web-test>
        [Test]
        public void 閉じるを押すと遷移を頼み状態が閉になると消える()
        {
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Chest());
            var shell = BlockInventoryTestShell.Create(this, state);
            simulate.FrameUpdate();

            simulate.Click(shell.View.Q<Button>("block-inventory-close").worldBound.center);
            Assert.AreEqual(1, shell.Commands.CloseCount);
            Assert.IsEmpty(shell.ShownToasts);

            state.Value = BlockInventoryPanelState.Closed;
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, shell.View.resolvedStyle.display);
        }

        [Test]
        public void 許可表に拒まれた閉じるはトーストを1回出しパネルを残す()
        {
            // ビルドメニューの × と同じく、開いているのに閉じられないことを知らせる（DC-13）
            // As with the build menu's close, tell the player the open panel could not be closed (DC-13)
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Chest());
            var shell = BlockInventoryTestShell.Create(this, state);
            shell.Commands.SetCloseResult(UiCommandRejected.Of(UiCommandRejectReason.ScreenCloseRefused));
            simulate.FrameUpdate();

            LogAssert.Expect(LogType.Warning, new Regex(@"\[UiCommandResultToaster\] ui_state\.request failed: transition_not_allowed"));
            simulate.Click(shell.View.Q<Button>("block-inventory-close").worldBound.center);

            Assert.AreEqual(1, shell.ShownToasts.Count);
            Assert.AreEqual("Could not close this screen right now", shell.ShownToasts[0].Text);
            Assert.AreEqual(GameUiToastVariant.Error, shell.ShownToasts[0].Variant);
            Assert.AreEqual(DisplayStyle.Flex, shell.View.resolvedStyle.display);
        }

        [Test]
        public void 閉からチェストを開いてもエラー行の文無しのLogErrorが出ない()
        {
            // エラー無しのパネルではエラー行を作らない（文無しの LocalizedLabel は 1 フレーム後に LogError）
            // No error row is built for an error-free panel (a text-less LocalizedLabel logs an error a frame later)
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var shell = BlockInventoryTestShell.Create(this, state);
            simulate.FrameUpdate();
            state.Value = BlockInventoryTestPanels.Chest();
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            Assert.IsNull(shell.View.Q("block-inventory-error"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}

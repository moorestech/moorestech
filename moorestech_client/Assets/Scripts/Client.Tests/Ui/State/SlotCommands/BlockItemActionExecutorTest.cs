using Client.Tests.Ui.State.Inventory;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands.Block;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.SlotCommands
{
    /// <summary>
    /// ブロック側の行為を Web ホスト（BlockInventoryActions）と同じ意味で実行する
    /// Executes block-side actions with the same meaning as the Web host (BlockInventoryActions)
    /// </summary>
    public class BlockItemActionExecutorTest
    {
        private static readonly ItemId Wood = new(1);
        private RecordingPlayerInventoryOperations _player;
        private RecordingBlockInventoryOperations _block;
        private BlockItemActionExecutor _executor;

        [SetUp]
        public void SetUp()
        {
            _player = new RecordingPlayerInventoryOperations(9, 2);
            _block = new RecordingBlockInventoryOperations { Slots = new[] { new SlotItem(Wood, 7), default, new SlotItem(Wood, 1) } };
            _executor = new BlockItemActionExecutor(_block, _player);
        }

        private UiCommandResult Run(PlannedItemAction plan)
        {
            Assert.IsTrue(BlockItemActionConversion.TryConvert(plan, out var action));
            return _executor.Execute(action);
        }

        [Test]
        public void プレイヤー側の計画は写せず偽()
        {
            var plan = PlannedItemAction.MoveItem(ItemMoveSlotRef.Main(0), ItemMoveSlotRef.Grab, 1);
            Assert.IsFalse(BlockItemActionConversion.TryConvert(plan, out var action));
            Assert.IsNull(action);
        }

        [Test]
        public void 掴み中の分割はSplitGrabNotEmptyで拒否する()
        {
            _player.SetGrab(new SlotItem(Wood, 1));
            var result = Run(PlannedItemAction.Split(ItemMoveSlotRef.Block(0)));
            Assert.AreEqual(UiCommandRejectReason.SplitGrabNotEmpty, ((UiCommandRejected)result).Reason);
            CollectionAssert.IsEmpty(_block.Moves);
        }

        [Test]
        public void 空の枠の分割はItemSourceSlotEmptyで拒否する()
        {
            var result = Run(PlannedItemAction.Split(ItemMoveSlotRef.Block(1)));
            Assert.AreEqual(UiCommandRejectReason.ItemSourceSlotEmpty, ((UiCommandRejected)result).Reason);
        }

        [Test]
        public void 1個の枠の分割は何も動かさず成功する()
        {
            var result = Run(PlannedItemAction.Split(ItemMoveSlotRef.Block(2)));
            Assert.IsInstanceOf<UiCommandAccepted>(result);
            CollectionAssert.IsEmpty(_block.Moves);
        }

        [Test]
        public void 移動の取りこぼしを理由へ写す()
        {
            var move = PlannedItemAction.MoveItem(ItemMoveSlotRef.Block(0), ItemMoveSlotRef.Grab, 1);
            _block.NextOutcome = PlayerItemMoveOutcome.SourceEmpty;
            Assert.AreEqual(UiCommandRejectReason.ItemSourceSlotEmpty, ((UiCommandRejected)Run(move)).Reason);
            _block.NextOutcome = PlayerItemMoveOutcome.SourceCountShort;
            Assert.AreEqual(UiCommandRejectReason.ItemSourceCountShort, ((UiCommandRejected)Run(move)).Reason);
        }

        [Test]
        public void 範囲外の枠はBlockSlotUnavailableで拒否する()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"^\[BlockItemActionExecutor\] block slot out of range"));
            var result = Run(PlannedItemAction.Collect(ItemMoveSlotRef.Block(3)));
            Assert.AreEqual(UiCommandRejectReason.BlockSlotUnavailable, ((UiCommandRejected)result).Reason);
            CollectionAssert.IsEmpty(_block.Collects);
        }
    }
}

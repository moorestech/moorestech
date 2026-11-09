using Client.Tests.Ui.State.Inventory;
using Client.Ui.State.Commands;
using Client.Tests.Ui.State.ItemMove;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands;
using Client.Ui.State.SlotCommands.Block;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.SlotCommands
{
    /// <summary>
    /// ブロックの枠の押下を Web の useBlockSlotGestures と同じ計画で実行器へ渡す（L0）
    /// Routes block-slot presses through the same plans as Web useBlockSlotGestures to the executors (L0)
    /// </summary>
    public class BlockSlotCommandsTest
    {
        private RecordingPlayerInventoryOperations _player;
        private RecordingBlockInventoryOperations _block;
        private BlockSlotCommands _commands;
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(System.Guid.Parse("00000000-0000-0000-1234-000000000001"));
            _player = new RecordingPlayerInventoryOperations(36, 2);
            _block = new RecordingBlockInventoryOperations { Slots = new[] { new SlotItem(_wood, 7), new SlotItem(_wood, 3) } };
            var runner = new PlannedItemActionRunner(new PlayerItemActionExecutor(_player), new BlockItemActionExecutor(_block, _player));
            _commands = new BlockSlotCommands(_block, _player, runner, new FixedMaxStackLookup(64));
        }

        /// <web-test file="e2e/tests/block/blockInventory.spec.ts">filled スロット左クリックで block→grab の move_item を送り、grab オーバーレイが出る</web-test>
        [Test]
        public void 中身のある枠の左押下は全量を掴む()
        {
            _commands.LeftDown(0, false);
            Assert.AreEqual((ItemMoveSlotRef.Block(0), ItemMoveSlotRef.Grab, 7), _block.Moves[0]);
        }

        /// <web-test file="e2e/tests/block/blockInventoryGestures.spec.ts">空手の右クリックで block_inventory.split を送り grab に半分入る</web-test>
        [Test]
        public void 空手の右押下は半分を掴む()
        {
            _commands.RightDown(0);
            Assert.AreEqual((ItemMoveSlotRef.Block(0), ItemMoveSlotRef.Grab, 3), _block.Moves[0]);
        }

        /// <web-test file="e2e/tests/block/blockInventoryGestures.spec.ts">grab 保持中の右クリックで block スロットへ1個置く</web-test>
        [Test]
        public void 掴み中の右押下は1個置く()
        {
            _player.SetGrab(new SlotItem(_wood, 7));
            _commands.RightDown(1);
            Assert.AreEqual((ItemMoveSlotRef.Grab, ItemMoveSlotRef.Block(1), 1), _block.Moves[0]);
        }

        /// <web-test file="e2e/tests/block/blockInventoryGestures.spec.ts">ダブルクリックで block_inventory.collect を送る</web-test>
        [Test]
        public void ダブルクリックは空手ならその枠へ掴み中なら持ち手へ集める()
        {
            _commands.DoubleClick(1);
            _player.SetGrab(new SlotItem(_wood, 1));
            _commands.DoubleClick(1);
            CollectionAssert.AreEqual(new[] { "block:1", "grab" }, _block.Collects);
        }

        /// <web-test file="e2e/tests/block/blockInventoryGestures.spec.ts">Shift+クリックで block→main へ配分移動する</web-test>
        [Test]
        public void Shift押下はブロックからメインへ配分する()
        {
            _commands.LeftDown(0, true);
            Assert.AreEqual(ItemMoveSlotRef.Block(0), _block.Moves[0].From);
            Assert.AreEqual(ItemMoveArea.Main, _block.Moves[0].To.Area);
            Assert.AreEqual(7, _block.Moves[0].Count);
        }

        [Test]
        public void 押すたびに口を読み直し直前の押下の結果で計画する()
        {
            // 置いた直後の右押下は、口が返す置いた後の値 10 の半分を掴む（読み元の判別は BlockSlotReaderTest。D-37）
            // A right press right after placing takes half of the post-write 10 the port returns (the read source is pinned by BlockSlotReaderTest; D-37)
            _block.WriteThrough = true;
            _block.WriteThroughItemId = _wood;
            _block.Slots = new[] { default(SlotItem), default(SlotItem) };
            _player.SetGrab(new SlotItem(_wood, 10));
            _commands.LeftDown(0, false);
            _player.SetGrab(default);
            _commands.RightDown(0);
            Assert.AreEqual((ItemMoveSlotRef.Block(0), ItemMoveSlotRef.Grab, 5), _block.Moves[1]);
        }

        [Test]
        public void 閉じたあとの古い押下はブロックの枠の取りこぼしで拒否し何も動かさない()
        {
            LogAssert.Expect(LogType.Warning, "[block_inventory.slot] stale press ignored: block slot 0 is not available");
            _block.Open = false;
            var result = _commands.LeftDown(0, false);
            Assert.AreEqual(UiCommandRejectReason.BlockSlotUnavailable, ((UiCommandRejected)result).Reason);
            CollectionAssert.IsEmpty(_block.Moves);
        }
    }
}

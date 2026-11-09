using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Game.InGame.UI.Notification;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.State.ItemMove;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.SlotCommands.Player;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.SlotCommands
{
    /// <summary>
    /// プレイヤーの枠の押下をブロックの開閉に応じた文脈で計画し、ブロック宛ての移動はブロック側の実行器へ渡す（L0）
    /// Plans player-slot presses with a context following the block's open state; moves into the block go to the block executor (L0)
    /// </summary>
    public class PlayerSlotCommandsTest
    {
        private RecordingPlayerInventoryOperations _player;
        private RecordingBlockInventoryOperations _block;
        private RecordingPlayerSlotDrag _drag;
        private ReactiveProperty<BlockInventoryPanelState> _panel;
        private ClientLocalNotificationSource _notifications;
        private PlayerSlotCommands _commands;
        private ItemId _wood;
        private FixedMaxStackLookup _stackLevels;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));
            _player = new RecordingPlayerInventoryOperations(36, 2);
            _block = new RecordingBlockInventoryOperations { Slots = new[] { new SlotItem(_wood, 1), new SlotItem(default, 0) } };
            _drag = new RecordingPlayerSlotDrag();
            _panel = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            _notifications = new ClientLocalNotificationSource();
            _stackLevels = new FixedMaxStackLookup(64);
            var runner = new PlannedItemActionRunner(new PlayerItemActionExecutor(_player), new BlockItemActionExecutor(_block, _player));
            var reader = new PlayerSlotContextReader(new FakePanelSource(_panel), _block, new BlockSlotRestrictionInputBuilder(Array.Empty<IBlockSlotRestrictionContributor>()), _stackLevels);
            _commands = new PlayerSlotCommands(_player, _drag, runner, reader, new ShiftMoveRefusalNotifier(_notifications));
        }

        /// <web-test file="e2e/tests/block/blockInventoryGestures.spec.ts">block 開時は main の Shift+クリックが block へ配分移動する</web-test>
        [Test]
        public void ブロックが開いていればメインのShift押下はブロックへ配分する()
        {
            _panel.Value = BlockInventoryTestPanels.ChestWithSlots(new SlotItem(_wood, 1), new SlotItem(default, 0));
            _player.SetMain(1, new SlotItem(_wood, 5));
            _commands.LeftDown(PlayerSlotRef.Main(1), true);
            Assert.AreEqual(ItemMoveSlotRef.Main(1), _block.Moves[0].From);
            Assert.AreEqual(ItemMoveArea.Block, _block.Moves[0].To.Area);
        }

        [Test]
        public void サーバーの通知前に続けてShift押下しても宛先は口の生の値で空き枠へ分かれる()
        {
            // パネルの状態は 2 枠とも空のまま、口だけが先書きで埋まる（読み元がパネルの ItemSlots に戻れば 2 回とも枠 0 で赤。D-37）
            // The panel state keeps both slots empty while only the port is pre-written (reading the panel ItemSlots again sends both to slot 0 and fails; D-37)
            var stone = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000002"));
            _panel.Value = BlockInventoryTestPanels.ChestWithSlots(new SlotItem(default, 0), new SlotItem(default, 0));
            _block.Slots = new[] { new SlotItem(default, 0), new SlotItem(default, 0) };
            _block.WriteThrough = true;
            _block.WriteThroughItemId = _wood;
            _player.SetMain(1, new SlotItem(_wood, 5));
            _player.SetMain(2, new SlotItem(stone, 3));
            _commands.LeftDown(PlayerSlotRef.Main(1), true);
            _commands.LeftDown(PlayerSlotRef.Main(2), true);
            Assert.AreEqual(ItemMoveSlotRef.Block(0), _block.Moves[0].To);
            Assert.AreEqual(ItemMoveSlotRef.Block(1), _block.Moves[1].To);
        }

        [Test]
        public void 掴み中に空の枠を左押下すると分割ドラッグを始める()
        {
            // 分割ドラッグの開始は「掴み中・非 Shift・空の枠」だけ（C4-e の PlayerSlotPlanner。空手で中身ありは全量掴み）
            // Split-drag starts only while holding, without Shift, on an empty slot (C4-e PlayerSlotPlanner; empty-handed on a filled slot picks it all up)
            _player.SetGrab(new SlotItem(_wood, 4));
            _commands.LeftDown(PlayerSlotRef.Main(0), false);
            Assert.AreEqual(PlayerSlotRef.Main(0), _drag.Begun[0]);
        }

        [Test]
        public void 取得失敗のブロックへのShift押下は断りを通知して送らない()
        {
            NotificationMessagePack notified = null;
            _notifications.OnNotification.Subscribe(m => notified = m);
            _panel.Value = BlockInventoryTestPanels.OpenFailedChest();
            _player.SetMain(1, new SlotItem(_wood, 5));
            _commands.LeftDown(PlayerSlotRef.Main(1), true);
            Assert.AreEqual("denied.inventoryOpenFailed", notified.MessageId);
            CollectionAssert.IsEmpty(_block.Moves);
            CollectionAssert.IsEmpty(_player.Moves);
        }

        [Test]
        public void 未知のItemIdの枠のShift押下はエラーログと拒否で止め通常の左押下は掴む()
        {
            // D-65: 計画器が配分の腕でだけ断る。断った時は拒否を返しトーストで知らせる
            // D-65: the planner refuses only in the allocation arm; a refusal returns a rejection the toast reports
            var unknown = new ItemId(9999);
            _stackLevels.SetUnknown(unknown);
            _panel.Value = BlockInventoryTestPanels.ChestWithSlots(new SlotItem(default, 0), new SlotItem(default, 0));
            _player.SetMain(1, new SlotItem(unknown, 5));
            LogAssert.Expect(LogType.Error, new Regex(@"\[PlayerSlotCommands\] unknown ItemId"));
            var shifted = _commands.LeftDown(PlayerSlotRef.Main(1), true);
            Assert.AreEqual(UiCommandRejectReason.ItemUnknownInSlot, ((UiCommandRejected)shifted).Reason);
            CollectionAssert.IsEmpty(_block.Moves);
            Assert.IsInstanceOf<UiCommandAccepted>(_commands.LeftDown(PlayerSlotRef.Main(1), false));
            CollectionAssert.AreEqual(new[] { $"{PlayerSlotRef.Main(1)}->Grab x5" }, _player.Moves);
        }

        [Test]
        public void ブロックが閉じていればメインの未知のIDのShift押下は拒否せず何もしない()
        {
            // D-65: ブロック閉の main 起点の Shift は配分先が無く計画器が何もしない。未知の ID でも拒否トーストを出さない
            // D-65: with the block closed a main-source Shift has no destination; even an unknown ID shows no rejection toast
            var unknown = new ItemId(9999);
            _stackLevels.SetUnknown(unknown);
            _player.SetMain(1, new SlotItem(unknown, 5));
            Assert.IsInstanceOf<UiCommandAccepted>(_commands.LeftDown(PlayerSlotRef.Main(1), true));
            CollectionAssert.IsEmpty(_player.Moves);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void エラー中の列車を開いたままのShiftなしの左押下は列車のワーニングを出さない()
        {
            // Shift 配分の無い押下では列車のエラーを言わない（言うのは Shift の押下だけ）
            // A press without a Shift allocation does not report the train error (only Shift presses do)
            _panel.Value = BlockInventoryTestPanels.Train(BlockInventoryPanelError.ContainerMissing);
            _block.Open = false;
            _player.SetMain(1, new SlotItem(_wood, 5));
            Assert.IsInstanceOf<UiCommandAccepted>(_commands.LeftDown(PlayerSlotRef.Main(1), false));
            CollectionAssert.AreEqual(new[] { $"{PlayerSlotRef.Main(1)}->Grab x5" }, _player.Moves);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void エラー中の列車へのShift押下は理由をワーニングログに残す()
        {
            // 列車には取得失敗の断りが無いので、状態の枠で計画したことをログで知らせる（Task 5 レビュー Minor）
            // Trains have no fetch-failure refusal, so log that the plan used the state slots (Task 5 review Minor)
            _panel.Value = BlockInventoryTestPanels.Train(BlockInventoryPanelError.ContainerMissing);
            _block.Open = false;
            _player.SetMain(1, new SlotItem(_wood, 5));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[inventory\.shift_move\] train car 1 is in error \(ContainerMissing\)"));
            _commands.LeftDown(PlayerSlotRef.Main(1), true);
            CollectionAssert.IsEmpty(_block.Moves);
        }

        [Test]
        public void 進入は分割ドラッグの口へ素通しする()
        {
            _commands.LeftEnter(PlayerSlotRef.Main(2));
            _commands.RightEnter(PlayerSlotRef.Main(3));
            Assert.AreEqual(PlayerSlotRef.Main(2), _drag.LeftEntered[0]);
            Assert.AreEqual(PlayerSlotRef.Main(3), _drag.RightEntered[0]);
        }

        [Test]
        public void 減った後の古い枠の押下は警告して何もしない()
        {
            var result = _commands.LeftDown(PlayerSlotRef.Main(99), false);
            Assert.IsInstanceOf<UiCommandAccepted>(result);
            CollectionAssert.IsEmpty(_player.Moves);
        }
    }
}

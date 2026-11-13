using System;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Inventory;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Grab;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の操作の判定と結果写像を確かめる
    /// Verifies special-family command checks and outcome mapping
    /// </summary>
    public class SpecialBlockCommandsTest
    {
        private static readonly Vector3Int Position = new(3, 0, 4);
        private RecordingSpecialRequestSender _sender;
        private ReactiveProperty<BlockInventoryPanelState> _open;
        private RecordingPlayerInventoryOperations _operations;
        private SpecialBlockCommands _commands;

        [SetUp]
        public void SetUp()
        {
            _sender = new RecordingSpecialRequestSender();
            _open = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            _operations = new RecordingPlayerInventoryOperations(36, 0);
            var grab = new PlayerGrabModel(_operations);
            grab.Initialize();
            _commands = new SpecialBlockCommands(new FakePanelSource(_open), _sender, grab);
        }

        [Test]
        public void 開いていなければ送らずに開いていない拒否を返す()
        {
            var result = _commands.SetElectricToGearMode(0).GetAwaiter().GetResult() as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.BlockNotOpen, result.Reason);
            Assert.AreEqual("block_not_open", result.ToWireCode());
            CollectionAssert.IsEmpty(_sender.Sent);
        }

        [Test]
        public void 種別の違うブロックには送らず種別違いの拒否を返す()
        {
            // 開いているので「ブロックが開いていません」の文言を引かない（wire は Web と同じ invalid_block_type）
            // The block is open, so the "block is not open" message must not be picked (wire stays invalid_block_type as on the Web)
            Open(BlockMasterElement.BlockTypeConst.FilterSplitter);
            var result = _commands.SendRocketCommand(RocketCommand.Build).GetAwaiter().GetResult() as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.BlockTypeMismatch, result.Reason);
            Assert.AreEqual("invalid_block_type", result.ToWireCode());
            Assert.AreNotEqual(UiCommandRejectReasonTraits.ToLocalizationKey(UiCommandRejectReason.BlockNotOpen).Key, UiCommandRejectReasonTraits.ToLocalizationKey(result.Reason).Key);
            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(result.Reason));
            CollectionAssert.IsEmpty(_sender.Sent);
        }

        [Test]
        public void フィルタ枠は掴んでいる物を設定し右押下は空にし掴みが空の左押下は送らない()
        {
            Open(BlockMasterElement.BlockTypeConst.FilterSplitter);
            _operations.SetGrab(new SlotItem(new ItemId(4), 3));
            Assert.AreSame(UiCommandAccepted.Instance, _commands.SetFilterItem(2, 1, false).GetAwaiter().GetResult());
            Assert.AreSame(UiCommandAccepted.Instance, _commands.SetFilterItem(2, 1, true).GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { $"filter_item {Position} 77 2 1 4", $"filter_item {Position} 77 2 1 {ItemMaster.EmptyItemId.AsPrimitive()}" }, _sender.Sent);

            _operations.SetGrab(new SlotItem(ItemMaster.EmptyItemId, 0));
            var noop = _commands.SetFilterItem(2, 1, false).GetAwaiter().GetResult() as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.FilterSplitterGrabEmpty, noop.Reason);
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(noop.Reason));
            Assert.AreEqual(2, _sender.Sent.Count);
        }

        [Test]
        public void モードと出力モードと接続と合図は位置とインスタンスを付けて送る()
        {
            Open(BlockMasterElement.BlockTypeConst.FilterSplitter);
            _commands.SetFilterMode(0, FilterSplitterMode.Blacklist).GetAwaiter().GetResult();
            Open(BlockMasterElement.BlockTypeConst.ElectricToGearGenerator);
            _commands.SetElectricToGearMode(1).GetAwaiter().GetResult();
            Open(BlockMasterElement.BlockTypeConst.SpaceElevatorBase);
            _commands.ConnectSpaceElevator().GetAwaiter().GetResult();
            Open(BlockMasterElement.BlockTypeConst.RocketLaunchPad);
            _commands.SendRocketCommand(RocketCommand.Launch).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { $"filter_mode {Position} 77 0 Blacklist", $"electric_to_gear {Position} 1", $"connect {Position} 77", $"rocket {Position} Launch 77" }, _sender.Sent);
        }

        [Test]
        public void 古い合図は良性で失敗は理由のコード付きの拒否()
        {
            Open(BlockMasterElement.BlockTypeConst.RocketLaunchPad);
            _sender.SetNextOutcome(SpecialBlockRequestOutcome.Stale("rocket_command_not_awaiting_build"));
            var stale = _commands.SendRocketCommand(RocketCommand.Build).GetAwaiter().GetResult() as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.SpecialBlockCommandStale, stale.Reason);
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(stale.Reason));
            Assert.AreEqual("rocket_command_not_awaiting_build", stale.ToWireCode());

            _sender.SetNextOutcome(SpecialBlockRequestOutcome.Failed("rocket_command_missing_materials"));
            var failed = _commands.SendRocketCommand(RocketCommand.Build).GetAwaiter().GetResult() as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.SpecialBlockRequestFailed, failed.Reason);
            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(failed.Reason));
            Assert.AreEqual("rocket_command_missing_materials", failed.ToWireCode());
        }

        [Test]
        public void 送信後に開いているブロックが変わったら分配器の応答は当てない()
        {
            var sent = new BlockInstanceId(77);
            var other = new OpenBlockInventoryPanel(BlockMasterElement.BlockTypeConst.FilterSplitter, Guid.Empty, default, new BlockInstanceId(78), Position, Array.Empty<SlotItem>(), BlockInventoryPanelError.None);
            var same = new OpenBlockInventoryPanel(BlockMasterElement.BlockTypeConst.FilterSplitter, Guid.Empty, default, sent, Position, Array.Empty<SlotItem>(), BlockInventoryPanelError.None);
            Assert.IsTrue(VanillaSpecialBlockRequestSender.IsStillOpen(same, sent));
            Assert.IsFalse(VanillaSpecialBlockRequestSender.IsStillOpen(other, sent));
            Assert.IsFalse(VanillaSpecialBlockRequestSender.IsStillOpen(BlockInventoryPanelState.Closed, sent));
        }

        private void Open(string blockType)
        {
            _open.Value = new OpenBlockInventoryPanel(blockType, Guid.Empty, default, new BlockInstanceId(77), Position, Array.Empty<SlotItem>(), BlockInventoryPanelError.None);
        }
    }
}

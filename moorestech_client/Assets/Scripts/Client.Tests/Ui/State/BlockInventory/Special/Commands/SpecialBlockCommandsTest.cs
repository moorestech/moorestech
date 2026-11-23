using System;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.State.Toast;
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
    /// 特殊族の操作の判定と結果写像を、トーストへ渡した結果で確かめる
    /// Verifies special-family command checks and outcome mapping through the results handed to the toaster
    /// </summary>
    public class SpecialBlockCommandsTest
    {
        private static readonly Vector3Int Position = new(3, 0, 4);
        private RecordingSpecialRequestSender _sender;
        private ReactiveProperty<BlockInventoryPanelState> _open;
        private RecordingPlayerInventoryOperations _operations;
        private SpecialBlockCommands _commands;
        private RecordingUiCommandResultToaster _toaster;

        [SetUp]
        public void SetUp()
        {
            _sender = new RecordingSpecialRequestSender();
            _open = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            _operations = new RecordingPlayerInventoryOperations(36, 0);
            var grab = new PlayerGrabModel(_operations);
            grab.Initialize();
            _toaster = new RecordingUiCommandResultToaster();
            _commands = new SpecialBlockCommands(new FakePanelSource(_open), _sender, grab, _toaster);
        }

        [Test]
        public void 開いていなければ送らずに開いていない拒否を返す()
        {
            _commands.SetElectricToGearMode(0);
            var result = LastReported(SpecialBlockCommands.SetElectricToGearModeCommandName) as UiCommandRejected;
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
            _commands.SendRocketCommand(RocketCommand.Build);
            var result = LastReported(SpecialBlockCommands.RocketCommandName) as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.BlockTypeMismatch, result.Reason);
            Assert.AreEqual("invalid_block_type", result.ToWireCode());
            Assert.AreNotEqual(UiCommandRejectReasonTable.Get(UiCommandRejectReason.BlockNotOpen).LocalizationKey.Key, UiCommandRejectReasonTable.Get(result.Reason).LocalizationKey.Key);
            Assert.IsFalse(UiCommandRejectReasonTable.Get(result.Reason).IsBenign);
            CollectionAssert.IsEmpty(_sender.Sent);
        }

        [Test]
        public void フィルタ枠は掴んでいる物を設定し右押下は空にし掴みが空の左押下は送らない()
        {
            Open(BlockMasterElement.BlockTypeConst.FilterSplitter);
            _operations.SetGrab(new SlotItem(new ItemId(4), 3));
            _commands.SetFilterItem(2, 1, false);
            Assert.AreSame(UiCommandAccepted.Instance, LastReported(SpecialBlockCommands.SetFilterItemCommandName));
            _commands.SetFilterItem(2, 1, true);
            Assert.AreSame(UiCommandAccepted.Instance, LastReported(SpecialBlockCommands.SetFilterItemCommandName));
            CollectionAssert.AreEqual(new[] { $"filter_item {Position} 77 2 1 4", $"filter_item {Position} 77 2 1 {ItemMaster.EmptyItemId.AsPrimitive()}" }, _sender.Sent);

            _operations.SetGrab(new SlotItem(ItemMaster.EmptyItemId, 0));
            _commands.SetFilterItem(2, 1, false);
            var noop = LastReported(SpecialBlockCommands.SetFilterItemCommandName) as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.FilterSplitterGrabEmpty, noop.Reason);
            Assert.IsTrue(UiCommandRejectReasonTable.Get(noop.Reason).IsBenign);
            Assert.AreEqual(2, _sender.Sent.Count);
        }

        [Test]
        public void モードと出力モードと接続と合図は位置とインスタンスを付けて送る()
        {
            Open(BlockMasterElement.BlockTypeConst.FilterSplitter);
            _commands.SetFilterMode(0, FilterSplitterMode.Blacklist);
            Open(BlockMasterElement.BlockTypeConst.ElectricToGearGenerator);
            _commands.SetElectricToGearMode(1);
            Open(BlockMasterElement.BlockTypeConst.SpaceElevatorBase);
            _commands.ConnectSpaceElevator();
            Open(BlockMasterElement.BlockTypeConst.RocketLaunchPad);
            _commands.SendRocketCommand(RocketCommand.Launch);

            CollectionAssert.AreEqual(new[] { $"filter_mode {Position} 77 0 Blacklist", $"electric_to_gear {Position} 1", $"connect {Position} 77", $"rocket {Position} Launch 77" }, _sender.Sent);

            // 操作ごとの名前で受理がトーストの係へ届く
            // Each acceptance reaches the toaster under its own command name
            CollectionAssert.AreEqual(new[] { SpecialBlockCommands.SetFilterModeCommandName, SpecialBlockCommands.SetElectricToGearModeCommandName, SpecialBlockCommands.ConnectSpaceElevatorCommandName, SpecialBlockCommands.RocketCommandName }, ReportedNames());
            foreach (var report in _toaster.Reports) Assert.AreSame(UiCommandAccepted.Instance, report.Result);
        }

        [Test]
        public void 古い合図は良性で失敗は理由のコード付きの拒否()
        {
            Open(BlockMasterElement.BlockTypeConst.RocketLaunchPad);
            _sender.SetNextOutcome(SpecialBlockRequestOutcome.Stale("rocket_command_not_awaiting_build"));
            _commands.SendRocketCommand(RocketCommand.Build);
            var stale = LastReported(SpecialBlockCommands.RocketCommandName) as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.SpecialBlockCommandStale, stale.Reason);
            Assert.IsTrue(UiCommandRejectReasonTable.Get(stale.Reason).IsBenign);
            Assert.AreEqual("rocket_command_not_awaiting_build", stale.ToWireCode());

            _sender.SetNextOutcome(SpecialBlockRequestOutcome.Failed("rocket_command_missing_materials"));
            _commands.SendRocketCommand(RocketCommand.Build);
            var failed = LastReported(SpecialBlockCommands.RocketCommandName) as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.SpecialBlockRequestFailed, failed.Reason);
            Assert.IsFalse(UiCommandRejectReasonTable.Get(failed.Reason).IsBenign);
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

        [TestCase(BlockMasterElement.BlockTypeConst.RocketLaunchPad)]
        [TestCase(BlockMasterElement.BlockTypeConst.SpaceElevatorBase)]
        public void 積荷の選択は開いた発射台か拠点の座標と実体で送り成功は受理になる(string blockType)
        {
            Open(blockType);
            _commands.SelectOrbitalCargo(new ItemId(10));
            CollectionAssert.AreEqual(new[] { $"cargo {Position} 77 10" }, _sender.Sent);
            Assert.IsInstanceOf<UiCommandAccepted>(LastReported(SpecialBlockCommands.SelectOrbitalCargoCommandName));
        }

        [Test]
        public void 積荷の選択はほかの種類のブロックへ送らずサーバーの拒否はコードつきの失敗になる()
        {
            Open(BlockMasterElement.BlockTypeConst.FilterSplitter);
            _commands.SelectOrbitalCargo(new ItemId(10));
            Assert.AreEqual(UiCommandRejectReason.BlockTypeMismatch, ((UiCommandRejected)LastReported(SpecialBlockCommands.SelectOrbitalCargoCommandName)).Reason);
            CollectionAssert.IsEmpty(_sender.Sent);

            Open(BlockMasterElement.BlockTypeConst.RocketLaunchPad);
            _sender.SetNextOutcome(SpecialBlockRequestOutcome.Failed("cargo_select_locked"));
            _commands.SelectOrbitalCargo(new ItemId(10));
            var rejected = (UiCommandRejected)LastReported(SpecialBlockCommands.SelectOrbitalCargoCommandName);
            Assert.AreEqual(UiCommandRejectReason.SpecialBlockRequestFailed, rejected.Reason);
            Assert.AreEqual("cargo_select_locked", rejected.ToWireCode());
        }

        // 最後にトーストへ渡った結果（名前も確かめる）
        // The last result handed to the toaster, checking its command name too
        private UiCommandResult LastReported(string expectedCommandName)
        {
            var last = _toaster.Reports[_toaster.Reports.Count - 1];
            Assert.AreEqual(expectedCommandName, last.CommandName);
            return last.Result;
        }

        private string[] ReportedNames()
        {
            var names = new string[_toaster.Reports.Count];
            for (var i = 0; i < names.Length; i++) names[i] = _toaster.Reports[i].CommandName;
            return names;
        }

        private void Open(string blockType)
        {
            _open.Value = new OpenBlockInventoryPanel(blockType, Guid.Empty, default, new BlockInstanceId(77), Position, Array.Empty<SlotItem>(), BlockInventoryPanelError.None);
        }
    }
}

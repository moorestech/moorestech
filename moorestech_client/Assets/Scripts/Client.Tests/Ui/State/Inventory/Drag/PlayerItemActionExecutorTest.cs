using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Inventory.Drag
{
    /// <summary>
    /// プレイヤー側の行為を Web のホスト（InventoryActions）と同じ意味で実行する
    /// Executes player-side actions with the same meaning as the Web host (InventoryActions)
    /// </summary>
    public class PlayerItemActionExecutorTest
    {
        private static readonly ItemId Wood = new(1);
        private static readonly ItemId Stone = new(2);
        private RecordingPlayerInventoryOperations _operations;
        private PlayerItemActionExecutor _executor;

        [SetUp]
        public void SetUp()
        {
            _operations = new RecordingPlayerInventoryOperations(9, 2);
            _executor = new PlayerItemActionExecutor(_operations);
        }

        [Test]
        public void 同じ枠への移動は何もせず受理する()
        {
            _operations.SetMain(0, new SlotItem(Wood, 10));

            var result = _executor.Execute(new PlayerItemAction[] { new MoveBetweenSlotsAction(PlayerSlotRef.Main(0), PlayerSlotRef.Main(0), 10) });

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            Assert.IsEmpty(_operations.Moves);
        }

        [Test]
        public void 別IDの枠への部分移動は何もせず受理する()
        {
            // 持ち物の口が NoEffect を返し（警告は口が出す）、Web と同じく拒否にはしない
            // The port returns NoEffect (it logs the warning) and, like Web, this is not a rejection
            _operations.SetMain(0, new SlotItem(Wood, 10));
            _operations.SetMain(1, new SlotItem(new ItemId(2), 3));

            var result = _executor.Execute(new PlayerItemAction[] { new MoveBetweenSlotsAction(PlayerSlotRef.Main(0), PlayerSlotRef.Main(1), 4) });

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            Assert.AreEqual(10, _operations.ReadMain(0).Count);
            Assert.AreEqual(3, _operations.ReadMain(1).Count);
        }

        [Test]
        public void 枠からgrabへ掴む()
        {
            _operations.SetMain(0, new SlotItem(Wood, 10));

            var result = _executor.Execute(new PlayerItemAction[] { new PickUpToGrabAction(PlayerSlotRef.Main(0), 10) });

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            Assert.AreEqual(10, _operations.ReadGrab().Count);
        }

        [TestCase(0, "empty_slot", UiCommandRejectReason.ItemSourceSlotEmpty)]
        [TestCase(3, "insufficient_count", UiCommandRejectReason.ItemSourceCountShort)]
        public void 移動元の拒否は良性の拒否理由になる(int sourceCount, string wireCode, UiCommandRejectReason reason)
        {
            if (0 < sourceCount) _operations.SetMain(0, new SlotItem(Wood, sourceCount));

            var rejected = (UiCommandRejected)_executor.Execute(new PlayerItemAction[] { new PickUpToGrabAction(PlayerSlotRef.Main(0), 5) });

            Assert.AreEqual(reason, rejected.Reason);
            Assert.AreEqual(wireCode, rejected.ToWireCode());
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(rejected.Reason));
        }

        [Test]
        public void 分割は半分をgrabへ移す()
        {
            _operations.SetMain(0, new SlotItem(Wood, 9));

            var result = _executor.Execute(new PlayerItemAction[] { new SplitToGrabAction(PlayerSlotRef.Main(0)) });

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            Assert.AreEqual(4, _operations.ReadGrab().Count);
            Assert.AreEqual(5, _operations.ReadMain(0).Count);
        }

        [Test]
        public void 一個の分割は何もせず受理する()
        {
            _operations.SetMain(0, new SlotItem(Wood, 1));

            var result = _executor.Execute(new PlayerItemAction[] { new SplitToGrabAction(PlayerSlotRef.Main(0)) });

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            Assert.IsEmpty(_operations.Moves);
        }

        [Test]
        public void grabを持っていると分割は良性の拒否()
        {
            _operations.SetMain(0, new SlotItem(Wood, 4));
            _operations.SetGrab(new SlotItem(Stone, 1));

            var result = (UiCommandRejected)_executor.Execute(new PlayerItemAction[] { new SplitToGrabAction(PlayerSlotRef.Main(0)) });

            Assert.AreEqual(UiCommandRejectReason.SplitGrabNotEmpty, result.Reason);
            Assert.AreEqual("grab_not_empty", result.ToWireCode());
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(result.Reason));
        }

        [Test]
        public void 空の枠の分割は良性の拒否()
        {
            var result = (UiCommandRejected)_executor.Execute(new PlayerItemAction[] { new SplitToGrabAction(PlayerSlotRef.Main(0)) });

            Assert.AreEqual(UiCommandRejectReason.ItemSourceSlotEmpty, result.Reason);
        }

        [Test]
        public void 範囲外の枠の分割はログを出して受理する()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"split on stale slot"));

            var result = _executor.Execute(new PlayerItemAction[] { new SplitToGrabAction(PlayerSlotRef.Main(30)) });

            Assert.IsInstanceOf<UiCommandAccepted>(result);
        }

        [Test]
        public void 集めはgrabを持っていればgrabへ空手ならクリックした枠へ()
        {
            _executor.Execute(new PlayerItemAction[] { new CollectAction(PlayerSlotRef.Main(2)) });
            _operations.SetGrab(new SlotItem(Wood, 1));
            _executor.Execute(new PlayerItemAction[] { new CollectAction(PlayerSlotRef.Main(2)) });

            Assert.AreEqual(new[] { PlayerSlotRef.Main(2).ToString(), "Grab" }, _operations.Collects.ToArray());
        }

        [Test]
        public void 最初の拒否で止めて後ろを実行しない()
        {
            var result = _executor.Execute(new PlayerItemAction[] { new PickUpToGrabAction(PlayerSlotRef.Main(0), 1), new CollectAction(PlayerSlotRef.Main(1)) });

            Assert.IsInstanceOf<UiCommandRejected>(result);
            Assert.IsEmpty(_operations.Collects);
        }

        [TestCase(UiCommandRejectReason.ItemSourceSlotEmpty, "empty_slot", true)]
        [TestCase(UiCommandRejectReason.ItemSourceCountShort, "insufficient_count", true)]
        [TestCase(UiCommandRejectReason.SplitGrabNotEmpty, "grab_not_empty", true)]
        [TestCase(UiCommandRejectReason.SplitDragNoValidSlots, "no_valid_slots", false)]
        public void 足した拒否理由の良性とWebの失敗コードはWebのBENIGN_ERRORSと同じ(UiCommandRejectReason reason, string wireCode, bool benign)
        {
            Assert.AreEqual(wireCode, UiCommandRejectReasonTraits.ToWireCode(reason));
            Assert.AreEqual(benign, UiCommandRejectReasonTraits.IsBenign(reason));
            Assert.DoesNotThrow(() => UiCommandRejectReasonTraits.ToLocalizationKey(reason));
        }
    }
}

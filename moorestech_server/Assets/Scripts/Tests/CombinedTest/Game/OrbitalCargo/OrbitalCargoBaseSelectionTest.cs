using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Inventory;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.OrbitalCargo;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.OrbitalCargo
{
    // 宇宙エレベーター拠点の積荷の選択（ADR 0106 7）
    // Space elevator base cargo selection (ADR 0106 7)
    public class OrbitalCargoBaseSelectionTest
    {
        [Test]
        public void 未選択の拠点は何も受け取らず接続の前後とも選択待ちを出す()
        {
            CreateServer();
            var elevatorBase = PlaceUnselectedBase(Vector3Int.zero);
            var inventory = Inventory(elevatorBase);
            var satellite = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 1);
            Assert.AreEqual(1, inventory.InsertItem(satellite).Count);
            Assert.IsFalse(inventory.InsertionCheck(new List<IItemStack> { satellite }));
            Assert.IsFalse(inventory.IsAllowedToPlace(SpaceElevatorBaseInventoryComponent.SatelliteSlot, satellite));

            // 接続できるケーブルがあっても、選ぶまでは選択待ちが最優先（ADR 0106 7）
            // Even with enough cable, waiting for a selection comes first until selected (ADR 0106 7)
            LaunchCables(100);
            AssertUnselected();
            // 旧セーブ由来の「接続済み・未選択」も同じ表示になる
            // A connected-but-unselected base from an old save shows the same
            elevatorBase.GetComponent<ISpaceElevatorBase>().MarkConnected();
            AssertUnselected();

            #region Internal

            void AssertUnselected()
            {
                var detail = ReadDetail(elevatorBase);
                Assert.AreEqual(SpaceElevatorBaseStatus.CargoNotSelected, detail.Status);
                Assert.IsNull(detail.SelectedCargoItemId);
                Assert.IsNull(detail.ClimbingCargoItemId);
            }

            #endregion
        }

        [Test]
        public void 選んだ積荷だけを受け取り昇らせる()
        {
            var elevatorBase = SetupConnected(100f, ForUnitTestItemId.TestElevatorSatellite);
            var inventory = Inventory(elevatorBase);
            Assert.AreEqual(3, inventory.InsertItem(ForUnitTestItemId.TestElevatorBulkPayload, 3).Count);
            Assert.AreEqual(0, inventory.InsertItem(ForUnitTestItemId.TestElevatorSatellite, 1).Count);

            // 昇っている間は今の便の積荷を状態に載せる
            // While climbing, the state carries the current flight's payload
            RunSeconds(0.5);
            var detail = ReadDetail(elevatorBase);
            Assert.AreEqual(SpaceElevatorBaseStatus.Climbing, detail.Status);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorSatellite, detail.SelectedCargoItemId);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorSatellite, detail.ClimbingCargoItemId);
            RunSeconds(2);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorReward, inventory.GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Id);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 選び直すと前の種類を返し返せない分は入口を塞ぐ(bool refundHasSpace)
        {
            var elevatorBase = SetupConnected(100f, ForUnitTestItemId.TestElevatorBulkPayload);
            var inventory = Inventory(elevatorBase);
            // 1便分を置き、昇降開始のティック前に積荷を選び直す
            // Insert a full flight and reselect before the next tick can start the climb
            Assert.AreEqual(0, inventory.InsertItem(ForUnitTestItemId.TestElevatorBulkPayload, 3).Count);
            var refund = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 1);
            if (!refundHasSpace) refund.SetItem(0, ForUnitTestItemId.ItemId1, 1);
            Assert.AreEqual(OrbitalCargoSelectionResult.Success, elevatorBase.GetComponent<IOrbitalCargoSelectorComponent>().SelectCargo(ForUnitTestItemId.TestElevatorSatellite, refund));

            if (refundHasSpace)
            {
                // 返せたら入口は空き、新しい種類を待つ
                // When refunded, the inlet is empty and waits for the new kind
                Assert.AreEqual(3, refund.InventoryItems.Where(item => item.Id == ForUnitTestItemId.TestElevatorBulkPayload).Sum(item => item.Count));
                Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, ReadDetail(elevatorBase).Status);
                return;
            }

            // 返せない分は入口に残り、取り出すまで塞がる（ADR 0106 7）
            // Leftovers stay in the inlet and block it until taken out (ADR 0106 7)
            LogAssert.Expect(LogType.Warning, new Regex("選んだ積荷ではないため昇れません"));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(3, inventory.GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.InletBlocked, ReadDetail(elevatorBase).Status);
            Assert.AreEqual(1, inventory.InsertItem(ForUnitTestItemId.TestElevatorSatellite, 1).Count);

            // 取り出せば新しい種類が入り、昇る
            // Once taken out, the new kind goes in and climbs
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, ServerContext.ItemStackFactory.CreatEmpty());
            Assert.AreEqual(0, inventory.InsertItem(ForUnitTestItemId.TestElevatorSatellite, 1).Count);
            RunSeconds(0.5);
            Assert.AreEqual(SpaceElevatorBaseStatus.Climbing, ReadDetail(elevatorBase).Status);
        }

        [Test]
        public void 昇降中に選び直しても今の便は元の積荷のまま届く()
        {
            var elevatorBase = SetupConnected(100f, ForUnitTestItemId.TestElevatorSatellite);
            var inventory = Inventory(elevatorBase);
            inventory.InsertItem(ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(0.5);
            OrbitalCargoTestUtil.SelectCargo(elevatorBase, ForUnitTestItemId.TestElevatorBulkPayload);

            // 選択だけが変わり、昇っている便は衛星のまま（ADR 0106 4・7）
            // Only the selection changes; the climbing flight stays the satellite (ADR 0106 4, 7)
            var detail = ReadDetail(elevatorBase);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorBulkPayload, detail.SelectedCargoItemId);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorSatellite, detail.ClimbingCargoItemId);
            RunSeconds(2);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorReward, inventory.GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Id);
            Assert.IsNull(ReadDetail(elevatorBase).ClimbingCargoItemId);
        }
    }
}

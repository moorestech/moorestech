using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.OrbitalCargo;
using Tests.Util.Rocket;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Game.OrbitalCargo
{
    // 発射台の積荷の選択（ADR 0106 1・2・4）
    // Launch pad cargo selection (ADR 0106 1, 2, 4)
    public class OrbitalCargoPadSelectionTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            // 初打ち上げ済みにして自動の建造で検証する
            // Mark the first launch done so building runs automatically
            ServerContext.GetService<RocketLaunchHistory>().RecordLaunch();
        }

        [Test]
        public void 未選択の台は資材も積荷も受け取らず建造しない()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            var blockInventory = pad.GetComponent<IBlockInventory>();
            var fuel = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.RocketFuelId, 3);

            // プレイヤー・ベルトのどちらの搬入も余りとして返る
            // Both player and belt insertion come back as remainders
            Assert.AreEqual(3, inventory.InsertItem(fuel).Count);
            Assert.AreEqual(3, blockInventory.InsertItem(fuel, InsertItemContext.Empty).Count);
            Assert.IsFalse(blockInventory.InsertionCheck(new List<IItemStack> { fuel }));
            Assert.IsFalse(inventory.IsAllowedToPlace(1, fuel));
            Assert.AreEqual(0, inventory.GetItem(1).Count);

            // 中身を直接置いても、選ぶまで建造しない
            // Even with contents written directly, nothing builds until selected
            Assert.IsTrue(RocketLaunchPadTestUtil.GetLayout(pad).TryGetInputSlot(ForUnitTestItemId.SatellitePayloadId, out var satelliteSlot));
            inventory.SetItem(0, ForUnitTestItemId.RocketCaseId, 2);
            inventory.SetItem(1, ForUnitTestItemId.RocketFuelId, 3);
            inventory.SetItem(2, ForUnitTestItemId.RocketComputerId, 1);
            inventory.SetItem(satelliteSlot, ForUnitTestItemId.SatellitePayloadId, 1);
            GameUpdater.RunFrames(3);
            var state = RocketLaunchPadTestUtil.ReadState(pad.GetComponent<RocketLaunchPadProcessorComponent>());
            Assert.AreEqual(RocketLaunchPadPhase.Idle, state.Phase);
            Assert.AreEqual(RocketLaunchPadWaitReason.CargoNotSelected, state.WaitReason);
            Assert.IsNull(state.SelectedCargoItemId);
            Assert.IsNull(state.LoadedCargoItemId);
        }

        [Test]
        public void 選ぶと資材と選んだ積荷だけを受け取る()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            OrbitalCargoTestUtil.SelectCargo(pad, ForUnitTestItemId.SatellitePayloadId);
            Assert.AreEqual(0, inventory.InsertItem(ForUnitTestItemId.RocketFuelId, 3).Count);
            Assert.AreEqual(0, inventory.InsertItem(ForUnitTestItemId.SatellitePayloadId, 1).Count);

            // 選んでいない積荷は入らず、その入れ口へも置けない
            // A non-selected payload neither inserts nor can be placed into its inlet
            Assert.AreEqual(5, inventory.InsertItem(ForUnitTestItemId.CablePayloadId, 5).Count);
            Assert.IsTrue(RocketLaunchPadTestUtil.GetLayout(pad).TryGetInputSlot(ForUnitTestItemId.CablePayloadId, out var cableSlot));
            Assert.IsFalse(inventory.IsAllowedToPlace(cableSlot, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.CablePayloadId, 1)));
            Assert.AreEqual(0, inventory.GetItem(cableSlot).Count);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 選び直すと前の種類を返し入らない分は枠に残る(bool refundHasSpace)
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.IsTrue(RocketLaunchPadTestUtil.GetLayout(pad).TryGetInputSlot(ForUnitTestItemId.SatellitePayloadId, out var satelliteSlot));
            OrbitalCargoTestUtil.SelectCargo(pad, ForUnitTestItemId.SatellitePayloadId);
            Assert.AreEqual(0, inventory.InsertItem(ForUnitTestItemId.SatellitePayloadId, 3).Count);
            var refund = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 1);
            if (!refundHasSpace) refund.SetItem(0, ForUnitTestItemId.ItemId1, 1);

            // 返せた分は選んだ人へ、返せない分は元の枠に残る（ADR 0106 4）
            // What can be returned goes to the selecting player; the rest stays in its slot (ADR 0106 4)
            var result = pad.GetComponent<IOrbitalCargoSelectorComponent>().SelectCargo(ForUnitTestItemId.CablePayloadId, refund);
            Assert.AreEqual(OrbitalCargoSelectionResult.Success, result);
            Assert.AreEqual(refundHasSpace ? 0 : 3, inventory.GetItem(satelliteSlot).Count);
            Assert.AreEqual(refundHasSpace ? 3 : 0, refund.InventoryItems.Where(item => item.Id == ForUnitTestItemId.SatellitePayloadId).Sum(item => item.Count));

            // 選び直した時点から新しい種類だけを受け取る
            // From the reselection on, only the new kind is accepted
            Assert.AreEqual(0, inventory.InsertItem(ForUnitTestItemId.CablePayloadId, 5).Count);
            Assert.AreEqual(1, inventory.InsertItem(ForUnitTestItemId.SatellitePayloadId, 1).Count);
        }

        [Test]
        public void 選び直しても今の便は元の積荷で打ち上げ次の便から切り替わる()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var arrivals = new List<OrbitalArrival>();
            using var subscription = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(arrivals.Add);
            RocketLaunchPadTestUtil.SelectCargoAndFillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Building, 5));

            // 建造中に選び直すと、選択だけが変わり今の便の積荷は残る（ADR 0106 4）
            // Reselecting while building changes only the selection; the current flight keeps its cargo (ADR 0106 4)
            OrbitalCargoTestUtil.SelectCargo(pad, ForUnitTestItemId.CablePayloadId);
            var state = RocketLaunchPadTestUtil.ReadState(processor);
            Assert.AreEqual(ForUnitTestItemId.CablePayloadId, state.SelectedCargoItemId);
            Assert.AreEqual(ForUnitTestItemId.SatellitePayloadId, state.LoadedCargoItemId);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, 60));
            Assert.AreEqual(ForUnitTestItemId.SatellitePayloadId, arrivals.Single().PayloadItemId);

            // 次の便は新しい種類で積む
            // The next flight loads the new kind
            RocketLaunchPadTestUtil.FillMaterials(pad);
            Assert.AreEqual(0, pad.GetComponent<IOpenableBlockInventoryComponent>().InsertItem(ForUnitTestItemId.CablePayloadId, 5).Count);
            for (var tick = 0; tick < 200 && arrivals.Count < 2; tick++) GameUpdater.RunFrames(1);
            Assert.AreEqual(ForUnitTestItemId.CablePayloadId, arrivals[1].PayloadItemId);
        }
    }
}

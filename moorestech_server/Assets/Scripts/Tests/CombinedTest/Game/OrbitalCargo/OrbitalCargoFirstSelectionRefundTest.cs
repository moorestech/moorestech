using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.Rocket;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;

namespace Tests.CombinedTest.Game.OrbitalCargo
{
    // 旧セーブなどで未選択の台に前から積荷が入っているとき、最初の選択でも選ばなかった種類を返す（ADR 0106 4）
    // When an unselected block already holds payloads (e.g. an old save), the first selection also returns the kinds not chosen (ADR 0106 4)
    public class OrbitalCargoFirstSelectionRefundTest
    {
        [Test]
        public void 発射台は最初の選択で選ばなかった積荷枠を返し選んだ枠は残す()
        {
            CreateServer();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            // 受け入れ判定を通らない SetItem で、旧セーブの中身を再現する
            // Reproduce old-save contents with SetItem, which skips the acceptance check
            inventory.SetItem(PayloadSlot(pad, ForUnitTestItemId.CablePayloadId), ForUnitTestItemId.CablePayloadId, 3);
            inventory.SetItem(PayloadSlot(pad, ForUnitTestItemId.SatellitePayloadId), ForUnitTestItemId.SatellitePayloadId, 1);

            var refund = NewRefund();
            Assert.AreEqual(OrbitalCargoSelectionResult.Success, pad.GetComponent<IOrbitalCargoSelectorComponent>().SelectCargo(ForUnitTestItemId.SatellitePayloadId, refund));
            Assert.AreEqual(3, Count(refund, ForUnitTestItemId.CablePayloadId));
            Assert.AreEqual(0, inventory.GetItem(PayloadSlot(pad, ForUnitTestItemId.CablePayloadId)).Count);
            Assert.AreEqual(1, inventory.GetItem(PayloadSlot(pad, ForUnitTestItemId.SatellitePayloadId)).Count);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 拠点は最初の選択で入口の別の種類を返し同じ種類は残す(bool sameKind)
        {
            CreateServer();
            var elevatorBase = PlaceUnselectedBase(Vector3Int.zero);
            var held = sameKind ? ForUnitTestItemId.TestElevatorSatellite : ForUnitTestItemId.TestElevatorBulkPayload;
            Inventory(elevatorBase).SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, held, 2);

            var refund = NewRefund();
            Assert.AreEqual(OrbitalCargoSelectionResult.Success, elevatorBase.GetComponent<IOrbitalCargoSelectorComponent>().SelectCargo(ForUnitTestItemId.TestElevatorSatellite, refund));
            Assert.AreEqual(sameKind ? 0 : 2, Count(refund, held));
            Assert.AreEqual(sameKind ? 2 : 0, Inventory(elevatorBase).GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot).Count);
        }

        private static int PayloadSlot(IBlock pad, ItemId payloadItemId)
        {
            var layout = RocketLaunchPadTestUtil.GetLayout(pad);
            return layout.PayloadSlots.Single(slot => layout.RequirementAt(slot).ItemId == payloadItemId);
        }

        private static OpenableInventoryItemDataStoreService NewRefund()
        {
            return new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
        }

        private static int Count(OpenableInventoryItemDataStoreService inventory, ItemId itemId)
        {
            return inventory.InventoryItems.Where(item => item.Id == itemId).Sum(item => item.Count);
        }
    }
}

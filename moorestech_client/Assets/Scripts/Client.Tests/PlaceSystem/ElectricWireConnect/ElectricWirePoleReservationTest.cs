using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 電柱の建設コスト予約を、production の入口をそのまま起動して検証する
    /// Verifies the pole construction cost reservation by driving the production entry point itself
    /// </summary>
    public class ElectricWirePoleReservationTest
    {
        // lengthPerUnit=1、単一素材×1
        // TestElectricWire: lengthPerUnit=1 with a single material x1
        private static readonly Guid WireConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid WireMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [SetUp]
        public void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        // 電柱の建設コストが同一アイテムなら、電線1本分の必要数へ予約として上乗せされる
        // A pole construction cost on the same item is added to the single wire's requirement as a reservation
        public void 電柱の建設コスト予約が電線の必要数へ上乗せされる()
        {
            var wireItemId = MasterHolder.ItemMaster.GetItemId(WireMaterialGuid);

            // 電線1＋電柱2で3必要なところを2しか持たない
            // 3 are needed (1 wire plus 2 for the pole) against only 2 held
            var preview = ElectricWireExtendPreviewCalculator.BuildNewPolePreview(1f, false, WireConnectToolGuid, BuildInventory(wireItemId, 2), new[] { (itemId: wireItemId, count: 2) });

            Assert.IsFalse(preview.IsPlaceable);
            Assert.AreEqual(ElectricWirePlacementFailureReason.NoWireItem, preview.Judgement.FailureReason);
            Assert.AreEqual(1, preview.MaterialShortages.Count);
            Assert.AreEqual(2, preview.MaterialShortages[0].Held);
            Assert.AreEqual(3, preview.MaterialShortages[0].Required);
        }

        [Test]
        // 財布が電柱コストを賄い予約が空なら、同じ所持で設置可になる
        // With the wallet covering the pole and an empty reservation, the same inventory becomes placeable
        public void 予約が空なら同じ所持で設置可になる()
        {
            var wireItemId = MasterHolder.ItemMaster.GetItemId(WireMaterialGuid);

            var preview = ElectricWireExtendPreviewCalculator.BuildNewPolePreview(1f, false, WireConnectToolGuid, BuildInventory(wireItemId, 2), Array.Empty<(ItemId itemId, int count)>());

            Assert.IsTrue(preview.IsPlaceable);
            Assert.IsEmpty(preview.MaterialShortages);
        }

        private static List<IItemStack> BuildInventory(ItemId itemId, int count)
        {
            return new List<IItemStack> { ServerContext.ItemStackFactory.Create(itemId, count) };
        }
    }
}

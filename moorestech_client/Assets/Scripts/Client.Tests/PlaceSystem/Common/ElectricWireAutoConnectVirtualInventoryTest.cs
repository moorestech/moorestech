using System;
using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using Tests.Module.TestMod;
using UniRx;

namespace Client.Tests.PlaceSystem.Common
{
    /// <summary>
    /// ドラッグ自動接続の仮想在庫が、建設コスト予約の上乗せとセル確定時の減算を行うことを検証する
    /// Verifies the auto-connect virtual inventory adds the construction reservation and subtracts it when a cell is placed
    /// </summary>
    public class ElectricWireAutoConnectVirtualInventoryTest
    {
        private static readonly Guid WireMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [SetUp]
        public void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        // 電柱の建設コストが電線と同一アイテムなら、必要数へ予約として上乗せされる
        // A pole construction cost on the same item as the wire is added to the requirement as a reservation
        public void 建設コスト予約は電線の必要数へ上乗せされる()
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(WireMaterialGuid);
            var inventory = new ElectricWireAutoConnectVirtualInventory(BuildInventory(itemId, 2), new[] { (itemId, count: 2) });
            var wireCost = BuildWireCost(itemId, 1);

            Assert.IsFalse(inventory.CanAfford(wireCost));

            var shortages = inventory.CalculateShortages(wireCost);
            Assert.AreEqual(1, shortages.Count);
            Assert.AreEqual(2, shortages[0].Held);
            Assert.AreEqual(3, shortages[0].Required);
        }

        [Test]
        // 財布が建設コストを賄い予約が空なら、同じ所持で設置可になる
        // With the wallet covering the construction cost and an empty reservation, the same holdings become placeable
        public void 予約が空なら同じ所持で設置可になる()
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(WireMaterialGuid);
            var inventory = new ElectricWireAutoConnectVirtualInventory(BuildInventory(itemId, 2), Array.Empty<(ItemId itemId, int count)>());

            Assert.IsTrue(inventory.CanAfford(BuildWireCost(itemId, 1)));
        }

        [Test]
        // セル確定では電線分と予約分の両方が減り、2セル目は賄えなくなる
        // Placing a cell consumes both the wire cost and the reservation, so the second cell no longer fits
        public void セル確定で電線分と予約分の両方が減る()
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(WireMaterialGuid);
            var inventory = new ElectricWireAutoConnectVirtualInventory(BuildInventory(itemId, 3), new[] { (itemId, count: 2) });
            var wireCost = BuildWireCost(itemId, 1);

            Assert.IsTrue(inventory.CanAfford(wireCost));

            inventory.ConsumePlacedCell(wireCost);

            Assert.IsFalse(inventory.CanAfford(wireCost));
            Assert.AreEqual(0, inventory.CalculateShortages(wireCost)[0].Held);
        }

        private static List<ConnectToolMaterialCost> BuildWireCost(ItemId itemId, int count)
        {
            return new List<ConnectToolMaterialCost> { new(itemId, count) };
        }

        private static StubLocalPlayerInventory BuildInventory(ItemId itemId, int count)
        {
            return new StubLocalPlayerInventory(ServerContext.ItemStackFactory.Create(itemId, count));
        }

        // 仮想在庫が読むのは列挙だけなので、所持アイテムを列挙するだけのスタブを使う
        // The virtual inventory only enumerates, so a stub that merely lists held items is enough
        private class StubLocalPlayerInventory : ILocalPlayerInventory
        {
            private readonly List<IItemStack> _items;
            private readonly Subject<int> _onItemChange = new();

            public StubLocalPlayerInventory(IItemStack itemStack)
            {
                _items = new List<IItemStack> { itemStack };
            }

            public IItemStack this[int index] => _items[index];
            public IObservable<int> OnItemChange => _onItemChange;
            public int Count => _items.Count;
            public int MainSlotCount => _items.Count;
            public bool IsItemExist(ItemId itemId, int itemSlot) => _items[itemSlot].Id == itemId;
            public IEnumerator<IItemStack> GetEnumerator() => _items.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}

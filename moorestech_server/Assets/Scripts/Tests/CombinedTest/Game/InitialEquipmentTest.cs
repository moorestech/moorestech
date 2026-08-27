using System;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class InitialEquipmentTest
    {
        private const int PlayerId = 0;

        // ForUnitTest items.json の initialEquipmentItems は Test1×1
        // ForUnitTest items.json declares initialEquipmentItems = Test1×1
        private static readonly Guid Test1Guid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 新規プレイヤーの装備スロット0に初期装備が入り選択済みになる()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();

            var equipment = inventoryDataStore.GetInventoryData(PlayerId).EquipmentInventory;

            var expectedId = MasterHolder.ItemMaster.GetItemId(Test1Guid);
            Assert.AreEqual(expectedId, equipment.GetItem(0).Id);
            Assert.AreEqual(1, equipment.GetItem(0).Count);
            Assert.AreEqual(expectedId, equipment.GetSelectedItem().Id);
        }

        [Test]
        public void セーブからロードしたプレイヤーには再投入しない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();

            // 初期装備を空にしてセーブし、ロード後に復活しないこと
            // Empty the initial equipment, save, and confirm it does not come back on load
            var equipment = inventoryDataStore.GetInventoryData(PlayerId).EquipmentInventory;
            equipment.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            var saved = inventoryDataStore.GetSaveJsonObject();

            inventoryDataStore.LoadPlayerInventory(saved);

            Assert.AreEqual(0, inventoryDataStore.GetInventoryData(PlayerId).EquipmentInventory.GetItem(0).Count);
        }
    }
}

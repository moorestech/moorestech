using Core.Master;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class PlayerInventorySlotLevelSaveLoadTest
    {
        // セーブ→ロードでレベルとインベントリサイズが復元される
        // Save then load restores the level and the inventory size
        [Test]
        public void SaveLoadRestoresSlotLevelTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<IPlayerInventorySlotLevelDataStore>();
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);

            store.UnlockLevel(1);
            inventory.MainOpenableInventory.SetItem(50, new ItemId(1), 3);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            var loadedStore = loadServiceProvider.GetService<IPlayerInventorySlotLevelDataStore>();
            Assert.AreEqual(1, loadedStore.CurrentLevel);

            var loadedInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);
            Assert.AreEqual(54, loadedInventory.MainOpenableInventory.GetSlotSize());
            Assert.AreEqual(3, loadedInventory.MainOpenableInventory.GetItem(50).Count);
        }

        // レベル情報のない旧セーブ（アイテム数45）はレベル0でもアイテムを失わない
        // Legacy saves without level info keep all 45 items even at level 0
        [Test]
        public void LoadLegacySaveWithoutLevelKeepsItemsTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);
            inventory.MainOpenableInventory.SetItem(44, new ItemId(1), 8);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // inventorySlotLevelキーを消して旧フォーマットを再現する
            // Strip the inventorySlotLevel key to emulate the legacy format
            var legacyJson = Newtonsoft.Json.Linq.JObject.Parse(saveJson);
            legacyJson.Remove("inventorySlotLevel");

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(legacyJson.ToString());

            var loadedInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);
            Assert.AreEqual(45, loadedInventory.MainOpenableInventory.GetSlotSize());
            Assert.AreEqual(8, loadedInventory.MainOpenableInventory.GetItem(44).Count);
        }
    }
}

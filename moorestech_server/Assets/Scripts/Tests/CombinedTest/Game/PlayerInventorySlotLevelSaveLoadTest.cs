using Core.Master;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Game.PlayerIdentity;
using NUnit.Framework;
using Tests.Util.PlayerIdentity;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class PlayerInventorySlotLevelSaveLoadTest
    {
        // セーブ→ロードでレベル/サイズ復元
        // Save then load restores level and size
        [Test]
        public void SaveLoadRestoresSlotLevelTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<IPlayerInventorySlotLevelDataStore>();
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);

            store.UnlockLevel(1);
            inventory.MainOpenableInventory.SetItem(50, new ItemId(1), 3);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            var loadedStore = loadServiceProvider.GetService<IPlayerInventorySlotLevelDataStore>();
            Assert.AreEqual(1, loadedStore.CurrentLevel);

            var loadedInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            Assert.AreEqual(54, loadedInventory.MainOpenableInventory.GetSlotSize());
            Assert.AreEqual(3, loadedInventory.MainOpenableInventory.GetItem(50).Count);
        }

        // 旧セーブ（レベル無し）でもアイテム維持
        // Legacy saves without level keep items
        [Test]
        public void LoadLegacySaveWithoutLevelKeepsItemsTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            inventory.MainOpenableInventory.SetItem(44, new ItemId(1), 8);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // キー削除で旧フォーマット再現
            // Strip the key to emulate legacy format
            var legacyJson = Newtonsoft.Json.Linq.JObject.Parse(saveJson);
            legacyJson.Remove("inventorySlotLevel");

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(legacyJson.ToString());

            var loadedInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            Assert.AreEqual(45, loadedInventory.MainOpenableInventory.GetSlotSize());
            Assert.AreEqual(8, loadedInventory.MainOpenableInventory.GetItem(44).Count);
        }
    }
}

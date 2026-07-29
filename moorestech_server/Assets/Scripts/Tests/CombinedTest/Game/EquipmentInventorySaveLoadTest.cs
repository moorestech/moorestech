using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class EquipmentInventorySaveLoadTest
    {
        private const int PlayerId = 0;

        [Test]
        public void 装備と選択インデックスがセーブロードで往復する()
        {
            var saveStore = CreateInventoryDataStore();
            var equipmentInventory = saveStore.GetInventoryData(PlayerId).EquipmentInventory;
            equipmentInventory.SetItem(1, ToolItemId(), 1);
            equipmentInventory.SetSelectedEquipmentIndex(2);

            // 実ファイルと同じくJSON文字列を経由させ、GUID保存であることまで確かめる
            // Go through a JSON string like the real save file so GUID-based persistence is covered
            var savedJson = JsonConvert.SerializeObject(saveStore.GetSaveJsonObject());
            var loadStore = CreateInventoryDataStore();
            loadStore.LoadPlayerInventory(JsonConvert.DeserializeObject<List<PlayerInventorySaveJsonObject>>(savedJson));

            var loadedEquipment = loadStore.GetInventoryData(PlayerId).EquipmentInventory;
            Assert.AreEqual(ToolItemId(), loadedEquipment.GetItem(1).Id);
            Assert.AreEqual(1, loadedEquipment.GetItem(1).Count);
            Assert.AreEqual(2, loadedEquipment.SelectedEquipmentIndex);
        }

        [Test]
        public void 装備フィールドの無い旧セーブは空装備で開始する()
        {
            var saveStore = CreateInventoryDataStore();
            saveStore.GetInventoryData(PlayerId).EquipmentInventory.SetItem(0, ToolItemId(), 1);

            // 装備フィールドを取り除いて旧セーブを再現する
            // Reproduce a legacy save by stripping the equipment fields
            var savedJson = JArray.FromObject(saveStore.GetSaveJsonObject());
            foreach (var playerSave in savedJson.Children<JObject>())
            {
                playerSave.Remove("EquipmentInventoryItems");
                playerSave.Remove("SelectedEquipmentIndex");
            }

            var loadStore = CreateInventoryDataStore();
            loadStore.LoadPlayerInventory(savedJson.ToObject<List<PlayerInventorySaveJsonObject>>());

            var loadedEquipment = loadStore.GetInventoryData(PlayerId).EquipmentInventory;
            Assert.AreEqual(MasterHolder.ToolMaster.EquipmentSlotCount, loadedEquipment.GetSlotSize());
            for (var slot = 0; slot < loadedEquipment.GetSlotSize(); slot++)
            {
                Assert.AreEqual(0, loadedEquipment.GetItem(slot).Count);
            }
            Assert.AreEqual(0, loadedEquipment.SelectedEquipmentIndex);
        }

        [Test]
        public void 装備できないセーブアイテムはメインインベントリへ退避する()
        {
            var saveStore = CreateInventoryDataStore();
            saveStore.GetInventoryData(PlayerId);
            var saveJsonObjects = saveStore.GetSaveJsonObject();

            // 非ツールと上限超過のツールが装備スロットに保存された壊れたセーブを作る
            // Build a corrupted save where a non-tool and an over-capped tool sit in equipment slots
            var itemStackFactory = ServerContext.ItemStackFactory;
            saveJsonObjects[0].EquipmentInventoryItems = new List<ItemStackSaveJsonObject>
            {
                new(itemStackFactory.Create(NonToolItemId(), 1)),
                new(itemStackFactory.Create(ToolItemId(), 3)),
                new(itemStackFactory.CreatEmpty()),
            };

            var loadStore = CreateInventoryDataStore();
            loadStore.LoadPlayerInventory(saveJsonObjects);
            var loadedInventory = loadStore.GetInventoryData(PlayerId);

            // 装備には受け入れ可能な1個だけが残る
            // Equipment keeps only the single acceptable item
            Assert.AreEqual(0, loadedInventory.EquipmentInventory.GetItem(0).Count);
            Assert.AreEqual(ToolItemId(), loadedInventory.EquipmentInventory.GetItem(1).Id);
            Assert.AreEqual(1, loadedInventory.EquipmentInventory.GetItem(1).Count);

            // 入らなかった分はメインインベントリに残り、消失しない
            // What did not fit stays in the main inventory instead of disappearing
            Assert.AreEqual(1, CountInMainInventory(loadedInventory, NonToolItemId()));
            Assert.AreEqual(2, CountInMainInventory(loadedInventory, ToolItemId()));
        }

        [Test]
        public void メインが満杯でも装備から退避したアイテムは消えない()
        {
            var saveStore = CreateInventoryDataStore();
            var mainInventory = saveStore.GetInventoryData(PlayerId).MainOpenableInventory;

            // 別アイテムを1個ずつ全スロットに置き、退避先の空きが一つも無い状態を作る
            // Place a different item in every slot so the fallback has nowhere to go
            var nonToolItemIds = NonToolItemIds(2);
            var fillerItemId = nonToolItemIds[0];
            for (var slot = 0; slot < mainInventory.GetSlotSize(); slot++) mainInventory.SetItem(slot, fillerItemId, 1);
            var filledSlotCount = mainInventory.GetSlotSize();

            // 装備できない非ツールが装備スロットに保存された壊れたセーブを作る
            // Build a corrupted save where a non-tool sits in an equipment slot
            var rejectedItemId = nonToolItemIds[1];
            var saveJsonObjects = saveStore.GetSaveJsonObject();
            saveJsonObjects[0].EquipmentInventoryItems = new List<ItemStackSaveJsonObject>
            {
                new(ServerContext.ItemStackFactory.Create(rejectedItemId, 1)),
            };

            var loadStore = CreateInventoryDataStore();
            loadStore.LoadPlayerInventory(saveJsonObjects);
            var loadedInventory = loadStore.GetInventoryData(PlayerId);

            // 満杯でもスロットが拡張され、既存アイテムも退避アイテムも1個も失われない
            // Slots expand even when full, so neither the existing items nor the fallback are lost
            Assert.AreEqual(filledSlotCount, CountInMainInventory(loadedInventory, fillerItemId));
            Assert.AreEqual(1, CountInMainInventory(loadedInventory, rejectedItemId));
        }

        private int CountInMainInventory(PlayerInventoryData playerInventoryData, ItemId itemId)
        {
            var mainInventory = playerInventoryData.MainOpenableInventory;
            var count = 0;
            for (var slot = 0; slot < mainInventory.GetSlotSize(); slot++)
            {
                if (mainInventory.GetItem(slot).Id == itemId) count += mainInventory.GetItem(slot).Count;
            }
            return count;
        }

        private IPlayerInventoryDataStore CreateInventoryDataStore()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        private ItemId ToolItemId()
        {
            return MasterHolder.ItemMaster.GetItemId(MasterHolder.ToolMaster.All[0].ToolItemGuid);
        }

        private ItemId NonToolItemId()
        {
            return NonToolItemIds(1)[0];
        }

        private List<ItemId> NonToolItemIds(int count)
        {
            return MasterHolder.ItemMaster.GetItemAllIds().Where(itemId => !MasterHolder.ToolMaster.IsTool(itemId)).Take(count).ToList();
        }
    }
}

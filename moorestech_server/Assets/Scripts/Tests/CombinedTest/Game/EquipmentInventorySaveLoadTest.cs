using System;
using System.Collections.Generic;
using Core.Item;
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

        // toolsに登録されていない通常アイテム(Test2)
        // A plain item (Test2) that is not registered in tools
        private static readonly Guid NonToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

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
        public void 非ツールも複数個の装備もセーブのまま復元する()
        {
            var saveStore = CreateInventoryDataStore();
            saveStore.GetInventoryData(PlayerId);
            var saveJsonObjects = saveStore.GetSaveJsonObject();

            // 非ツールと2個以上のツールが装備スロットに入ったセーブを作る
            // Build a save where a non-tool and a stack of two or more tools sit in equipment slots
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

            // 受入制限が無いため、セーブ内容がそのまま装備スロットへ戻る
            // Without acceptance restrictions the save content is restored into the equipment slots as is
            var loadedEquipment = loadedInventory.EquipmentInventory;
            Assert.AreEqual(NonToolItemId(), loadedEquipment.GetItem(0).Id);
            Assert.AreEqual(1, loadedEquipment.GetItem(0).Count);
            Assert.AreEqual(ToolItemId(), loadedEquipment.GetItem(1).Id);
            Assert.AreEqual(3, loadedEquipment.GetItem(1).Count);
            Assert.AreEqual(0, loadedEquipment.GetItem(2).Count);

            // メインインベントリへの退避は起きない
            // Nothing falls back into the main inventory
            Assert.AreEqual(0, CountInMainInventory(loadedInventory, NonToolItemId()));
            Assert.AreEqual(0, CountInMainInventory(loadedInventory, ToolItemId()));
        }

        [Test]
        public void 装備スロットに入り切らないセーブでもアイテムが消えない()
        {
            var saveStore = CreateInventoryDataStore();
            saveStore.GetInventoryData(PlayerId);
            var saveJsonObjects = saveStore.GetSaveJsonObject();

            // マスタのスロット数を超える装備を持つセーブを作り、スロット縮小と同じ状況を再現する
            // Build a save holding more equipment than master's slot count, reproducing a shrunk slot count
            var itemStackFactory = ServerContext.ItemStackFactory;
            var slotCount = MasterHolder.ToolMaster.EquipmentSlotCount;
            var savedEquipmentItems = new List<ItemStackSaveJsonObject>();
            for (var slot = 0; slot < slotCount; slot++)
                savedEquipmentItems.Add(new ItemStackSaveJsonObject(itemStackFactory.Create(ToolItemId(), 1)));
            const int OverflowCount = 2;
            for (var overflow = 0; overflow < OverflowCount; overflow++)
                savedEquipmentItems.Add(new ItemStackSaveJsonObject(itemStackFactory.Create(NonToolItemId(), 1)));
            saveJsonObjects[0].EquipmentInventoryItems = savedEquipmentItems;

            var loadStore = CreateInventoryDataStore();
            loadStore.LoadPlayerInventory(saveJsonObjects);
            var loadedInventory = loadStore.GetInventoryData(PlayerId);

            // 入る分は装備へ、あふれた分はメインへ退避し、総数が保存される
            // What fits stays in equipment and the rest falls back to main, preserving the total count
            var loadedEquipment = loadedInventory.EquipmentInventory;
            for (var slot = 0; slot < slotCount; slot++)
            {
                Assert.AreEqual(ToolItemId(), loadedEquipment.GetItem(slot).Id);
                Assert.AreEqual(1, loadedEquipment.GetItem(slot).Count);
            }
            Assert.AreEqual(slotCount, loadedEquipment.GetSlotSize());
            Assert.AreEqual(OverflowCount, CountInMainInventory(loadedInventory, NonToolItemId()));
            Assert.AreEqual(0, CountInMainInventory(loadedInventory, ToolItemId()));
        }

        [Test]
        public void メインが満杯のセーブでも装備あふれ分だけ枠が伸びてアイテムが残る()
        {
            var saveStore = CreateInventoryDataStore();
            var savedMainInventory = saveStore.GetInventoryData(PlayerId).MainOpenableInventory;

            // 全スロットを最大スタックで埋め、あふれ装備の行き先が1枠も無いセーブを作る
            // Fill every slot to max stack so the save leaves no room at all for the overflowing equipment
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(ToolItemId());
            var mainSlotCount = savedMainInventory.GetSlotSize();
            for (var slot = 0; slot < mainSlotCount; slot++) savedMainInventory.SetItem(slot, ToolItemId(), maxStack);

            var saveJsonObjects = saveStore.GetSaveJsonObject();
            var itemStackFactory = ServerContext.ItemStackFactory;
            var savedEquipmentItems = new List<ItemStackSaveJsonObject>();
            for (var slot = 0; slot < MasterHolder.ToolMaster.EquipmentSlotCount; slot++)
                savedEquipmentItems.Add(new ItemStackSaveJsonObject(itemStackFactory.Create(ToolItemId(), 1)));
            const int OverflowCount = 2;
            for (var overflow = 0; overflow < OverflowCount; overflow++)
                savedEquipmentItems.Add(new ItemStackSaveJsonObject(itemStackFactory.Create(NonToolItemId(), 1)));
            saveJsonObjects[0].EquipmentInventoryItems = savedEquipmentItems;

            var loadStore = CreateInventoryDataStore();
            loadStore.LoadPlayerInventory(saveJsonObjects);
            var loadedInventory = loadStore.GetInventoryData(PlayerId);

            // 満杯のメインはあふれ分だけ末尾へ伸び、退避先が確保される
            // The full main inventory grows by exactly the overflow count so the fallback has room
            var loadedMainInventory = loadedInventory.MainOpenableInventory;
            Assert.AreEqual(mainSlotCount + OverflowCount, loadedMainInventory.GetSlotSize());
            Assert.AreEqual(OverflowCount, CountInMainInventory(loadedInventory, NonToolItemId()));
            Assert.AreEqual(mainSlotCount * maxStack, CountInMainInventory(loadedInventory, ToolItemId()));
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
            return MasterHolder.ItemMaster.GetItemId(NonToolItemGuid);
        }
    }
}

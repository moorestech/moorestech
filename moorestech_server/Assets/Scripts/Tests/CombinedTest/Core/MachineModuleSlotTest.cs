using System;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    ///     機械のモジュールスロット（第3レンジ）の挙動を検証するテスト
    ///     Tests verifying the behavior of machine module slots (the third slot range)
    /// </summary>
    public class MachineModuleSlotTest
    {
        // テスト用機械のスロット構成（blocks.jsonのTestElectricMachine / TestGearMachineに対応）
        // Slot layout of the test machines (matches TestElectricMachine / TestGearMachine in blocks.json)
        private const int InputSlotCount = 2;
        private const int OutputSlotCount = 3;
        private const int ModuleSlotCount = 4;
        private const int ModuleRangeStart = InputSlotCount + OutputSlotCount;

        [Test]
        // モジュールスロットがインプット・アウトプットの後ろの第3レンジとして存在することを確認する
        // Verify module slots exist as the third range after input and output
        public void ThirdRangeExistsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 機械を設置してスロット数を確認
            // Place the machine and check the slot size
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(InputSlotCount + OutputSlotCount + ModuleSlotCount, inventory.GetSlotSize());

            // モジュールアイテムを先頭のモジュールスロットにセットして取得できることを確認
            // Set a module item into the first module slot and verify it can be retrieved
            var moduleItem = CreateModuleItem(1);
            inventory.SetItem(ModuleRangeStart, moduleItem);
            Assert.AreEqual(moduleItem, inventory.GetItem(ModuleRangeStart));
            Assert.AreEqual(moduleItem, inventory.InventoryItems[ModuleRangeStart]);

            // 末尾のモジュールスロット（統合スロットの最終番号）にもアクセスできることを確認
            // Verify the last module slot (final unified slot index) is also accessible
            var lastModuleSlot = ModuleRangeStart + ModuleSlotCount - 1;
            var lastModuleItem = CreateModuleItem(3);
            inventory.SetItem(lastModuleSlot, lastModuleItem);
            Assert.AreEqual(lastModuleItem, inventory.GetItem(lastModuleSlot));
            Assert.AreEqual(lastModuleItem, inventory.InventoryItems[lastModuleSlot]);

            // 最終アウトプットスロットへのセットがモジュールレンジへ流れないことを確認
            // Verify a set to the last output slot does not route into the module range
            var lastOutputSlot = InputSlotCount + OutputSlotCount - 1;
            var outputItem = ServerContext.ItemStackFactory.Create(new ItemId(3), 7);
            inventory.SetItem(lastOutputSlot, outputItem);
            Assert.AreEqual(outputItem, inventory.GetItem(lastOutputSlot));

            // リフレクションでアウトプットサブインベントリの実体に入っていることを確認
            // Verify via reflection that the item landed in the actual output sub-inventory
            var outputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inventory);
            Assert.AreEqual(outputItem, outputInventory.OutputSlot[OutputSlotCount - 1]);

            // 設定した2つのモジュールスロット以外のモジュールレンジは空のまま
            // The module range except the two configured slots stays empty
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(ModuleRangeStart + 1).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(ModuleRangeStart + 2).Id);
        }

        [Test]
        // 搬送由来のInsertItemはインプットレンジのみに入り、モジュールスロットには入らないことを確認する
        // Verify transport-driven InsertItem only fills the input range and never module slots
        public void TransportInsertDoesNotFillModuleSlotsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // モジュールアイテムを搬送経由で挿入してもインプットスロットに入ることを確認
            // Inserting a module item via transport lands in an input slot
            var moduleItem = CreateModuleItem(1);
            var remainder = inventory.InsertItem(moduleItem);
            Assert.AreEqual(0, remainder.Count);
            Assert.AreEqual(moduleItem, inventory.GetItem(0));
            AssertModuleRangeIsEmpty(inventory);

            // インプットを満杯にしてさらに挿入しても、モジュールスロットへ溢れないことを確認
            // Fill the input range completely; further inserts must not overflow into module slots
            var item1MaxStack = MasterHolder.ItemMaster.GetItemMaster(new ItemId(1)).MaxStack;
            var item2MaxStack = MasterHolder.ItemMaster.GetItemMaster(new ItemId(2)).MaxStack;
            inventory.SetItem(0, itemStackFactory.Create(new ItemId(1), item1MaxStack));
            inventory.SetItem(1, itemStackFactory.Create(new ItemId(2), item2MaxStack));

            var overflowRemainder = inventory.InsertItem(itemStackFactory.Create(new ItemId(3), 5));
            Assert.AreEqual(5, overflowRemainder.Count);
            AssertModuleRangeIsEmpty(inventory);
        }

        [Test]
        // モジュールスロットがセーブ・ロードで維持され、moduleSlotキーの無い過去セーブも読めることを確認する
        // Verify module slots survive save/load and that old saves without the moduleSlot key still load
        public void ModuleSaveLoadRoundTripTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // モジュールスロットにアイテムを入れてセーブする
            // Put an item into a module slot and save
            var moduleItem = CreateModuleItem(2);
            inventory.SetItem(ModuleRangeStart, moduleItem);
            var saveState = block.GetSaveState();

            // ロード後もモジュールスロットの内容が維持されていることを確認
            // Verify the module slot content is retained after loading
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            var loadedBlock = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(100), saveState, block.BlockPositionInfo);
            var loadedInventory = loadedBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(moduleItem, loadedInventory.GetItem(ModuleRangeStart));

            // moduleSlotキーを取り除いた「過去セーブ」をロードしてもモジュールスロットが空で読めることを確認
            // Loading an "old save" with the moduleSlot key removed must yield empty module slots without errors
            var saveKey = typeof(VanillaMachineSaveComponent).FullName;
            var machineJson = JObject.Parse(saveState[saveKey]);
            machineJson.Remove("moduleSlot");
            saveState[saveKey] = machineJson.ToString();

            var oldSaveBlock = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(101), saveState, block.BlockPositionInfo);
            var oldSaveInventory = oldSaveBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            AssertModuleRangeIsEmpty(oldSaveInventory);
        }

        [Test]
        // インベントリ整理でモジュールスロットが除外されることと、ギア機械にもモジュールスロットがあることを確認する
        // Verify inventory sorting excludes module slots and that gear machines also have module slots
        public void SortExcludesModuleSlotsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // インプットをItemId降順で配置し、モジュールスロットにモジュールアイテムを装着する
            // Place input items in descending ItemId order and equip a module item into a module slot
            inventory.SetItem(0, itemStackFactory.Create(new ItemId(5), 3));
            inventory.SetItem(1, itemStackFactory.Create(new ItemId(2), 4));
            var moduleItem = CreateModuleItem(1);
            inventory.SetItem(ModuleRangeStart, moduleItem);

            InventorySortService.Sort(inventory, inventory.GetSortExcludedSlots());

            // インプット・アウトプットレンジはソートされ、モジュールスロットはそのまま残ることを確認
            // Input/output ranges are sorted while module slots stay untouched
            Assert.AreEqual(itemStackFactory.Create(new ItemId(2), 4), inventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(5), 3), inventory.GetItem(1));
            Assert.AreEqual(moduleItem, inventory.GetItem(ModuleRangeStart));
            for (var i = ModuleRangeStart + 1; i < ModuleRangeStart + ModuleSlotCount; i++)
            {
                Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(i).Id);
            }

            // ギア機械にもモジュールスロットの第3レンジが存在することを確認
            // Verify the gear machine also has the third module slot range
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMachine, new Vector3Int(5, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gearBlock);
            var gearInventory = gearBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(InputSlotCount + OutputSlotCount + ModuleSlotCount, gearInventory.GetSlotSize());
        }

        // テスト用モジュールアイテム（modules.jsonの定義に対応するアイテム）を生成する
        // Create a test module item (the item linked to a modules.json definition)
        private static IItemStack CreateModuleItem(int count)
        {
            var moduleElement = MasterHolder.ModuleMaster.Modules.Data[0];
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(moduleElement.ItemGuid);
            return ServerContext.ItemStackFactory.Create(moduleItemId, count);
        }

        // モジュールレンジの全スロットが空であることを検証する
        // Assert that every slot in the module range is empty
        private static void AssertModuleRangeIsEmpty(VanillaMachineBlockInventoryComponent inventory)
        {
            for (var i = ModuleRangeStart; i < ModuleRangeStart + ModuleSlotCount; i++)
            {
                Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(i).Id);
            }
        }
    }
}

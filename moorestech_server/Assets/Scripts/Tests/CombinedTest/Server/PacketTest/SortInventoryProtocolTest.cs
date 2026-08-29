using System;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.SortInventoryProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class SortInventoryProtocolTest
    {
        private const int PlayerId = 0;

        [Test]
        public void MainInventorySortTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // 分割アイテムを末尾込みで配置
            // Place scattered and split items, including the trailing slots.
            mainInventory.SetItem(0, new ItemId(2), 7);
            mainInventory.SetItem(2, new ItemId(3), 5);
            mainInventory.SetItem(5, new ItemId(1), 4);
            mainInventory.SetItem(8, new ItemId(1), 6);
            mainInventory.SetItem(mainInventory.GetSlotSize() - 1, new ItemId(5), 9);

            // メインインベントリを整理
            // Sort the main inventory.
            packet.GetPacketResponse(GetPacket(InventoryIdentifierMessagePack.CreateMainMessage(PlayerId)), new PacketResponseContext(null));

            // 同種結合しId昇順に再配置
            // Same items are merged and re-packed in ItemId ascending order (trailing slots included too).
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 10), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(2), 7), mainInventory.GetItem(1));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(3), 5), mainInventory.GetItem(2));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(5), 9), mainInventory.GetItem(3));

            // 余ったスロットは空になっている
            // Remaining slots are emptied.
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(4).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(mainInventory.GetSlotSize() - 1).Id);
        }

        [Test]
        public void MainInventoryStackOverflowMergeTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // 合計が最大スタックを超える同種アイテムを2スロットに分割配置する
            // Place a same item split across two slots so the total exceeds the max stack.
            var itemId = new ItemId(2);
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(itemId);
            mainInventory.SetItem(0, itemId, maxStack - 5);
            mainInventory.SetItem(3, itemId, 10);

            packet.GetPacketResponse(GetPacket(InventoryIdentifierMessagePack.CreateMainMessage(PlayerId)), new PacketResponseContext(null));

            // 先頭スロットは最大スタックまで詰まり、あふれた5個が次スロットへ流れる
            // The first slot fills to max stack and the overflowing 5 items flow into the next slot.
            Assert.AreEqual(itemStackFactory.Create(itemId, maxStack), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(itemId, 5), mainInventory.GetItem(1));
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(2).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(3).Id);
        }

        [Test]
        public void BlockInventorySortTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            var chestPosition = new Vector3Int(5, 10);
            worldDataStore.TryAddBlock(ForUnitTestModBlockId.ChestId, chestPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
            var chestComponent = chest.GetComponent<VanillaChestComponent>();

            // チェスト（itemSlotCount=5, slot0-4）へバラけた・分割されたアイテムを配置する
            // Place scattered and split items into the chest (itemSlotCount=5, slots 0-4).
            chestComponent.SetItem(1, new ItemId(2), 5);
            chestComponent.SetItem(2, new ItemId(1), 4);
            chestComponent.SetItem(4, new ItemId(1), 6);

            // チェスト（サブインベントリ）を整理
            // Sort the chest (sub-inventory).
            packet.GetPacketResponse(GetPacket(InventoryIdentifierMessagePack.CreateBlockMessage(chestPosition)), new PacketResponseContext(null));

            // 同種結合＋ItemId 昇順（ホットバー除外なし、全スロット対象）
            // Same items merged and re-packed in ItemId order (no hotbar exclusion; all slots).
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 10), chestComponent.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(2), 5), chestComponent.GetItem(1));
            Assert.AreEqual(ItemMaster.EmptyItemId, chestComponent.GetItem(2).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, chestComponent.GetItem(4).Id);
        }

        [Test]
        // 旧仕様は入出力レンジをソートしモジュールだけ除外したが、ADR 0042で全スロットがレシピ束縛されソート自体が完全なno-opになった
        // The old spec sorted the input/output range and excluded only modules; ADR 0042 binds every slot to the recipe, making sorting a full no-op
        public void MachineInventorySortIsNoOpTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // 機械（input=2, output=3, module=4）を設置し、レシピを選択してから束縛先スロットへ素材を配置する
            // Place the machine (input=2, output=3, module=4), select the recipe, then place materials into their bound slots.
            var machinePosition = new Vector3Int(5, 10);
            worldDataStore.TryAddBlock(ForUnitTestModBlockId.MachineId, machinePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            var machineComponent = machine.GetComponent<VanillaMachineBlockInventoryComponent>();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid);
            MachineRecipeSelectTestUtil.SelectRecipe(machine, recipe);
            var input0 = itemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid), 3);
            var input1 = itemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(recipe.InputItems[1].ItemGuid), 4);
            machineComponent.SetItem(0, input0);
            machineComponent.SetItem(1, input1);

            // モジュールレンジの先頭と末尾（slot5・slot8）にモジュールアイテムを装着する
            // Equip module items into the first and last module slots (slot 5 and slot 8).
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(MasterHolder.ItemMaster.Items.Modules.First().ItemGuid);
            var firstModuleItem = itemStackFactory.Create(moduleItemId, 1);
            var lastModuleItem = itemStackFactory.Create(moduleItemId, 2);
            machineComponent.SetItem(5, firstModuleItem);
            machineComponent.SetItem(8, lastModuleItem);

            // 実プロトコル経由で機械インベントリを整理する
            // Sort the machine inventory via the actual protocol packet.
            packet.GetPacketResponse(GetPacket(InventoryIdentifierMessagePack.CreateBlockMessage(machinePosition)), new PacketResponseContext(null));

            // 全スロットが束縛済みのため、ソート後も入出力レンジの配置は一切変わらない
            // Every slot is bound, so the input/output range is untouched after sorting.
            Assert.AreEqual(input0, machineComponent.GetItem(0));
            Assert.AreEqual(input1, machineComponent.GetItem(1));
            Assert.AreEqual(ItemMaster.EmptyItemId, machineComponent.GetItem(2).Id);

            // モジュールスロットも整理対象外なので位置も中身も不動
            // Module slots are also excluded from sorting and stay in place untouched.
            Assert.AreEqual(firstModuleItem, machineComponent.GetItem(5));
            Assert.AreEqual(ItemMaster.EmptyItemId, machineComponent.GetItem(6).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, machineComponent.GetItem(7).Id);
            Assert.AreEqual(lastModuleItem, machineComponent.GetItem(8));
        }

        [Test]
        public void EquipmentInventoryIsExcludedFromSortTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var equipmentInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // マスタの初期装備が前方スロットを埋めるため、空き前提を作り直してから検証する
            // The master's initial equipment fills the front slots, so rebuild the empty precondition first
            for (var slot = 0; slot < equipmentInventory.GetSlotSize(); slot++)
                equipmentInventory.SetItem(slot, itemStackFactory.CreatEmpty());

            // 前方に空きを残して装備を置き、選択インデックス2が指す中身を固定する
            // Leave the front slots empty so the content pointed at by selected index 2 is pinned
            var lastSlot = equipmentInventory.GetSlotSize() - 1;
            equipmentInventory.SetItem(lastSlot, new ItemId(2), 3);
            equipmentInventory.SetSelectedEquipmentIndex(lastSlot);

            // 装備識別子はプロトコル上そのまま解決されるため、除外宣言が無いと実際に整理されてしまう
            // The equipment identifier resolves as-is in the protocol, so without an exclusion it really would be tidied
            packet.GetPacketResponse(GetPacket(InventoryIdentifierMessagePack.CreateEquipmentMessage(PlayerId)), new PacketResponseContext(null));

            // 詰め直されると選択インデックスが空スロットを指すことになる
            // Re-packing would leave the selected index pointing at an empty slot
            Assert.AreEqual(itemStackFactory.Create(new ItemId(2), 3), equipmentInventory.GetItem(lastSlot));
            Assert.AreEqual(ItemMaster.EmptyItemId, equipmentInventory.GetItem(0).Id);
            Assert.AreEqual(new ItemId(2), equipmentInventory.GetSelectedItem().Id);
        }

        private byte[] GetPacket(InventoryIdentifierMessagePack target)
        {
            return MessagePackSerializer.Serialize(new SortInventoryProtocolMessagePack(target));
        }
    }
}

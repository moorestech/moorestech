using System.Linq;
using Core.Master;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.InventoryItemMoveProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class EquipmentInventoryTest
    {
        private const int PlayerId = 0;

        [Test]
        public void ツールは装備スロットへ1枠1個ずつ入る()
        {
            var (packet, playerInventory) = CreateServerWithPlayerInventory();
            var toolItemId = ToolItemId();

            // 全スロットを埋めてなお余る数をマスタから決める
            // Derive an amount from master that fills every slot and still has leftovers
            var insertCount = MasterHolder.ToolMaster.EquipmentSlotCount + 2;
            playerInventory.MainOpenableInventory.SetItem(0, toolItemId, insertCount);

            // Insert経路では各スロットに1個ずつ入り、入りきらない分はメインに残る
            // The insert path puts one item per slot and leaves the rest in main
            packet.GetPacketResponse(MoveItemPacket(insertCount, 0, 0, ItemMoveType.InsertSlot), new PacketResponseContext(null));

            var equipmentInventory = playerInventory.EquipmentInventory;
            Assert.AreEqual(MasterHolder.ToolMaster.EquipmentSlotCount, equipmentInventory.GetSlotSize());
            for (var slot = 0; slot < equipmentInventory.GetSlotSize(); slot++)
            {
                Assert.AreEqual(toolItemId, equipmentInventory.GetItem(slot).Id);
                Assert.AreEqual(1, equipmentInventory.GetItem(slot).Count);
            }
            Assert.AreEqual(insertCount - equipmentInventory.GetSlotSize(), playerInventory.MainOpenableInventory.GetItem(0).Count);
        }

        [Test]
        public void 非ツールは挿入経路で装備スロットに1個も入らない()
        {
            var (packet, playerInventory) = CreateServerWithPlayerInventory();
            var nonToolItemId = NonToolItemId();
            playerInventory.MainOpenableInventory.SetItem(0, nonToolItemId, 5);

            packet.GetPacketResponse(MoveItemPacket(5, 0, 0, ItemMoveType.InsertSlot), new PacketResponseContext(null));

            Assert.AreEqual(5, playerInventory.MainOpenableInventory.GetItem(0).Count);
            for (var slot = 0; slot < playerInventory.EquipmentInventory.GetSlotSize(); slot++)
            {
                Assert.AreEqual(0, playerInventory.EquipmentInventory.GetItem(slot).Count);
            }
        }

        [Test]
        public void 空の装備スロットへの入れ替え指定は1個だけ受け取り残りは戻る()
        {
            var (packet, playerInventory) = CreateServerWithPlayerInventory();
            var toolItemId = ToolItemId();
            playerInventory.MainOpenableInventory.SetItem(0, toolItemId, 4);

            // 移動先が空のためSwapSlot指定でも入れ替えではなくReplaceItem経路を通る
            // The destination is empty, so SwapSlot still goes through the ReplaceItem path instead of a swap
            packet.GetPacketResponse(MoveItemPacket(4, 0, 0, ItemMoveType.SwapSlot), new PacketResponseContext(null));

            Assert.AreEqual(1, playerInventory.EquipmentInventory.GetItem(0).Count);
            Assert.AreEqual(3, playerInventory.MainOpenableInventory.GetItem(0).Count);
        }

        [Test]
        public void 非ツールは入れ替え経路でも装備スロットに入らない()
        {
            var (packet, playerInventory) = CreateServerWithPlayerInventory();
            var toolItemId = ToolItemId();
            var nonToolItemId = NonToolItemId();

            // 装備済みツールと非ツールを全数入れ替えようとする
            // Try to swap an equipped tool with a non-tool in full
            playerInventory.EquipmentInventory.SetItem(0, toolItemId, 1);
            playerInventory.MainOpenableInventory.SetItem(0, nonToolItemId, 1);
            packet.GetPacketResponse(MoveItemPacket(1, 0, 0, ItemMoveType.SwapSlot), new PacketResponseContext(null));

            Assert.AreEqual(toolItemId, playerInventory.EquipmentInventory.GetItem(0).Id);
            Assert.AreEqual(nonToolItemId, playerInventory.MainOpenableInventory.GetItem(0).Id);
        }

        [Test]
        public void 選択インデックスは素手を含む範囲にクランプされる()
        {
            var (_, playerInventory) = CreateServerWithPlayerInventory();
            var equipmentInventory = playerInventory.EquipmentInventory;
            var toolItemId = ToolItemId();
            equipmentInventory.SetItem(1, toolItemId, 1);

            // スロット数を超える指定は末尾スロットへ、負値は素手(-1)へ丸める
            // Indexes beyond the slot count clamp to the last slot and negatives clamp to bare hands (-1)
            equipmentInventory.SetSelectedEquipmentIndex(99);
            Assert.AreEqual(equipmentInventory.GetSlotSize() - 1, equipmentInventory.SelectedEquipmentIndex);
            equipmentInventory.SetSelectedEquipmentIndex(-5);
            Assert.AreEqual(-1, equipmentInventory.SelectedEquipmentIndex);

            // 素手のときは空スタック、装備スロット選択時はそのアイテムを返す
            // Bare hands returns an empty stack while a selected slot returns its item
            Assert.AreEqual(0, equipmentInventory.GetSelectedItem().Count);
            equipmentInventory.SetSelectedEquipmentIndex(1);
            Assert.AreEqual(toolItemId, equipmentInventory.GetSelectedItem().Id);
        }

        private (PacketResponseCreator packet, PlayerInventoryData playerInventory) CreateServerWithPlayerInventory()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return (packet, serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId));
        }

        private byte[] MoveItemPacket(int count, int fromMainSlot, int toEquipmentSlot, ItemMoveType itemMoveType)
        {
            return MessagePackSerializer.Serialize(new InventoryItemMoveProtocolMessagePack(
                count, itemMoveType,
                InventoryIdentifierMessagePack.CreateMainMessage(PlayerId), fromMainSlot,
                InventoryIdentifierMessagePack.CreateEquipmentMessage(PlayerId), toEquipmentSlot));
        }

        private ItemId ToolItemId()
        {
            return MasterHolder.ItemMaster.GetItemId(MasterHolder.ToolMaster.All[0].ToolItemGuid);
        }

        private ItemId NonToolItemId()
        {
            return MasterHolder.ItemMaster.GetItemAllIds().First(itemId => !MasterHolder.ToolMaster.IsTool(itemId));
        }
    }
}

using System;
using Core.Master;
using Game.Context;
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
    /// <summary>
    ///     装備インベントリが受入制限を持たない通常のインベントリとして振る舞うことを検証する
    ///     Verify the equipment inventory behaves as a plain inventory without acceptance restrictions
    /// </summary>
    public class EquipmentInventoryTest
    {
        private const int PlayerId = 0;

        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        // 受入制限が無いことを検証する通常アイテム
        // Plain item used to verify that no acceptance restriction exists
        private static readonly Guid NonToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        [Test]
        public void ツールは装備スロットへスタックして入る()
        {
            var (packet, playerInventory) = CreateServerWithPlayerInventory();
            var toolItemId = ToolItemId();

            // 装備スロット数より多い個数でも、スタック上限までは1スロットに収まる
            // Even more than the slot count fits into a single slot up to the stack limit
            var insertCount = MasterHolder.ItemMaster.Items.EquipmentSlotCount + 2;
            playerInventory.MainOpenableInventory.SetItem(0, toolItemId, insertCount);

            packet.GetPacketResponse(MoveItemPacket(insertCount, 0, 0, ItemMoveType.InsertSlot), new PacketResponseContext(null));

            var equipmentInventory = playerInventory.EquipmentInventory;
            Assert.AreEqual(MasterHolder.ItemMaster.Items.EquipmentSlotCount, equipmentInventory.GetSlotSize());
            Assert.AreEqual(toolItemId, equipmentInventory.GetItem(0).Id);
            Assert.AreEqual(insertCount, equipmentInventory.GetItem(0).Count);
            Assert.AreEqual(0, playerInventory.MainOpenableInventory.GetItem(0).Count);
        }

        [Test]
        public void 非ツールも挿入経路で装備スロットに入る()
        {
            var (packet, playerInventory) = CreateServerWithPlayerInventory();
            var nonToolItemId = NonToolItemId();
            playerInventory.MainOpenableInventory.SetItem(0, nonToolItemId, 5);

            packet.GetPacketResponse(MoveItemPacket(5, 0, 0, ItemMoveType.InsertSlot), new PacketResponseContext(null));

            Assert.AreEqual(nonToolItemId, playerInventory.EquipmentInventory.GetItem(0).Id);
            Assert.AreEqual(5, playerInventory.EquipmentInventory.GetItem(0).Count);
            Assert.AreEqual(0, playerInventory.MainOpenableInventory.GetItem(0).Count);
        }

        [Test]
        public void 空の装備スロットへの入れ替え指定は全数を受け取る()
        {
            var (packet, playerInventory) = CreateServerWithPlayerInventory();
            var toolItemId = ToolItemId();
            playerInventory.MainOpenableInventory.SetItem(0, toolItemId, 4);

            // 移動先が空のためSwapSlot指定でも入れ替えではなくReplaceItem経路を通る
            // The destination is empty, so SwapSlot still goes through the ReplaceItem path instead of a swap
            packet.GetPacketResponse(MoveItemPacket(4, 0, 0, ItemMoveType.SwapSlot), new PacketResponseContext(null));

            Assert.AreEqual(4, playerInventory.EquipmentInventory.GetItem(0).Count);
            Assert.AreEqual(0, playerInventory.MainOpenableInventory.GetItem(0).Count);
        }

        [Test]
        public void 非ツールは入れ替え経路でも装備スロットに入る()
        {
            var (packet, playerInventory) = CreateServerWithPlayerInventory();
            var toolItemId = ToolItemId();
            var nonToolItemId = NonToolItemId();

            // 装備済みツールと非ツールを全数入れ替える
            // Swap an equipped tool with a non-tool in full
            playerInventory.EquipmentInventory.SetItem(0, toolItemId, 1);
            playerInventory.MainOpenableInventory.SetItem(0, nonToolItemId, 1);
            packet.GetPacketResponse(MoveItemPacket(1, 0, 0, ItemMoveType.SwapSlot), new PacketResponseContext(null));

            Assert.AreEqual(nonToolItemId, playerInventory.EquipmentInventory.GetItem(0).Id);
            Assert.AreEqual(toolItemId, playerInventory.MainOpenableInventory.GetItem(0).Id);
        }

        [Test]
        public void 選択インデックスは実スロット範囲にクランプされる()
        {
            var (_, playerInventory) = CreateServerWithPlayerInventory();
            var equipmentInventory = playerInventory.EquipmentInventory;
            var toolItemId = ToolItemId();
            equipmentInventory.SetItem(1, toolItemId, 1);

            // スロット数を超える指定は末尾スロットへ、負値は先頭(0)へ丸める
            // Indexes beyond the slot count clamp to the last slot and negatives clamp to the first slot (0)
            equipmentInventory.SetSelectedEquipmentIndex(99);
            Assert.AreEqual(equipmentInventory.GetSlotSize() - 1, equipmentInventory.SelectedEquipmentIndex);
            equipmentInventory.SetSelectedEquipmentIndex(-5);
            Assert.AreEqual(0, equipmentInventory.SelectedEquipmentIndex);

            // 空スロット選択時は空スタック、ツールのあるスロット選択時はそのアイテムを返す
            // An empty selected slot returns an empty stack while a slot holding a tool returns its item
            Assert.AreEqual(0, equipmentInventory.GetSelectedItem().Count);
            equipmentInventory.SetSelectedEquipmentIndex(1);
            Assert.AreEqual(toolItemId, equipmentInventory.GetSelectedItem().Id);
        }

        private (PacketResponseCreator packet, PlayerInventoryData playerInventory) CreateServerWithPlayerInventory()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);

            // マスタの初期装備が入った状態を前提にしないよう、装備スロットを空にしてから検証する
            // Clear the equipment slots so these cases do not depend on the master's initial equipment
            var equipmentInventory = playerInventory.EquipmentInventory;
            for (var slot = 0; slot < equipmentInventory.GetSlotSize(); slot++)
                equipmentInventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());

            return (packet, playerInventory);
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
            return MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
        }

        private ItemId NonToolItemId()
        {
            return MasterHolder.ItemMaster.GetItemId(NonToolItemGuid);
        }
    }
}

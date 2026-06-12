using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.ItemsModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.InventoryItemMoveProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     既存の移動プロトコルだけでモジュールスロット（統合インベントリ第3レンジ）へ装着できることを検証するテスト
    ///     Tests verifying modules can be equipped into module slots (third unified range) via the existing move protocol alone
    /// </summary>
    public class MachineModuleEquipMoveProtocolTest
    {
        private const int PlayerId = 0;

        // テスト用機械のスロット構成（blocks.jsonのTestElectricMachineに対応）
        // Slot layout of the test machine (matches TestElectricMachine in blocks.json)
        private const int InputSlotCount = 2;
        private const int OutputSlotCount = 3;
        private const int ModuleSlotCount = 4;
        private const int ModuleRangeStart = InputSlotCount + OutputSlotCount;

        [Test]
        // メインインベントリから移動プロトコルでモジュールを装着し、効果集計と取得プロトコルに反映されることを確認する
        // Equip a module from the main inventory via the move protocol and verify effect aggregation and the request protocol reflect it
        public void EquipModuleViaMoveProtocolTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;

            // 機械を設置し、スピードモジュールをメインインベントリのスロット0へ置く
            // Place the machine and put a speed module into main inventory slot 0
            var machinePos = new Vector3Int(1, 1, 1);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, machinePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var speedModuleItem = MasterHolder.ItemMaster.Items.Data.First(i => i.ModuleParam?.EffectAxis == ModuleParam.EffectAxisConst.Speed);
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(speedModuleItem.ItemGuid);
            mainInventory.SetItem(0, moduleItemId, 1);

            // 既存の移動プロトコル（InventoryType.Block＋スロット番号）でモジュールレンジ先頭スロットへ移動する
            // Move into the first module-range slot via the existing move protocol (InventoryType.Block + slot number)
            packet.GetPacketResponse(MovePacket(1,
                InventoryIdentifierMessagePack.CreateMainMessage(PlayerId), 0,
                InventoryIdentifierMessagePack.CreateBlockMessage(machinePos), ModuleRangeStart), new PacketResponseContext());

            // ブロック側のモジュールスロットに装着され、メイン側は空になっていることを確認
            // Verify the block module slot holds the module and the main slot is now empty
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(moduleItemId, inventory.GetItem(ModuleRangeStart).Id);
            Assert.AreEqual(1, inventory.GetItem(ModuleRangeStart).Count);
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(0).Id);

            // 効果コンポーネントの集計に装着モジュールが反映される（速度モジュールで加工時間倍率が1未満）
            // The effect component aggregation reflects the equipped module (speed module shortens the time multiplier)
            var effect = block.GetComponent<MachineModuleEffectComponent>().AggregateCurrent();
            Assert.Less(effect.ProcessingTimeMultiplier, 1f);

            // 既存のInventoryRequestProtocolのレスポンスにもモジュールスロットの内容が含まれることを確認
            // Verify the existing InventoryRequestProtocol response also contains the module slot content
            var response = RequestBlockInventory();
            Assert.AreEqual(InputSlotCount + OutputSlotCount + ModuleSlotCount, response.Items.Length);
            Assert.AreEqual(moduleItemId, response.Items[ModuleRangeStart].Id);
            Assert.AreEqual(1, response.Items[ModuleRangeStart].Count);

            #region Internal

            // ブロックインベントリの取得プロトコルを実行してレスポンスを返す
            // Execute the block inventory request protocol and return the response
            InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack RequestBlockInventory()
            {
                var identifier = InventoryIdentifierMessagePack.CreateBlockMessage(machinePos);
                var request = MessagePackSerializer.Serialize(new InventoryRequestProtocol.RequestInventoryRequestProtocolMessagePack(identifier));
                return MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(packet.GetPacketResponse(request, new PacketResponseContext())[0]);
            }

            #endregion
        }

        [Test]
        // 非モジュールアイテムも設計どおりモジュールスロットへ移動でき、アイテムは消えず効果集計だけが中立のままであることを確認する
        // A non-module item also moves into a module slot by design; it is not lost and only the effect aggregation stays neutral
        public void NonModuleItemMoveKeepsItemAndNeutralEffectTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;

            // 機械を設置し、非モジュールアイテム（ItemId4）をメインインベントリへ置く
            // Place the machine and put a non-module item (ItemId 4) into the main inventory
            var machinePos = new Vector3Int(1, 1, 1);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, machinePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var nonModuleItemId = new ItemId(4);
            mainInventory.SetItem(0, nonModuleItemId, 3);

            // モジュールレンジ2番目のスロットへ移動する（挿入ガードは設けない設計）
            // Move into the second module-range slot (no insertion guard by design)
            packet.GetPacketResponse(MovePacket(3,
                InventoryIdentifierMessagePack.CreateMainMessage(PlayerId), 0,
                InventoryIdentifierMessagePack.CreateBlockMessage(machinePos), ModuleRangeStart + 1), new PacketResponseContext());

            // アイテムはスロットに存在し続け、ロストしていないことを確認
            // Verify the item remains in the slot and is not lost
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(nonModuleItemId, inventory.GetItem(ModuleRangeStart + 1).Id);
            Assert.AreEqual(3, inventory.GetItem(ModuleRangeStart + 1).Count);
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(0).Id);

            // 効果集計は非モジュールを無視し中立のままであることを確認
            // Verify the effect aggregation ignores the non-module item and stays neutral
            var effect = block.GetComponent<MachineModuleEffectComponent>().AggregateCurrent();
            Assert.AreEqual(1f, effect.ProcessingTimeMultiplier, 0.0001f);
            Assert.AreEqual(1f, effect.PowerMultiplier, 0.0001f);
            Assert.AreEqual(0f, effect.ExtraOutputChance, 0.0001f);
        }

        // 移動プロトコルのリクエストパケットを生成する
        // Build a move protocol request packet
        private static byte[] MovePacket(int count, InventoryIdentifierMessagePack from, int fromSlot, InventoryIdentifierMessagePack to, int toSlot)
        {
            return MessagePackSerializer.Serialize(
                new InventoryItemMoveProtocolMessagePack(count, ItemMoveType.SwapSlot, from, fromSlot, to, toSlot));
        }
    }
}

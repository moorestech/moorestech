using Core.Update;
using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Game.UnlockState;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public abstract class ElectricWireAutoConnectPlaceTestBase
    {
        protected const int PlayerId = 5;
        // electricWire connectToolのrequiredItem（消費対象）
        // The electricWire connectTool's required item (the consumed material)
        protected static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        protected static readonly Guid ElectricWireConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");

        #region TestUtil

        protected static (PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        protected static IOpenableInventory SetupWire(ServiceProvider serviceProvider, int wireCount)
        {
            // 自動接続にはelectricWire connectToolの解放が必要
            // Auto-connect requires the electricWire connectTool to be unlocked
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ElectricWireConnectToolGuid);

            // 電線アイテムだけをインベントリへ置く（設置ブロックは建設コスト方式のため所持不要）
            // Put only wire items into the inventory (placement no longer consumes a block item)
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            if (0 < wireCount) inventory.SetItem(10, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(WireItemGuid), wireCount));

            return inventory;
        }

        protected static void GrantRequiredItems(ServiceProvider serviceProvider, BlockId blockId)
        {
            // 建設コスト1セット分をインベントリへ投入する
            // Insert one construction-cost set into the inventory
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            foreach (var requiredItem in MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems)
            {
                inventory.InsertItem(MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid), (int)requiredItem.Count);
            }
        }

        protected static void UnlockBlock(ServiceProvider serviceProvider, BlockId blockId)
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlock(blockGuid);
        }

        protected static void PlaceBlock(PacketResponseCreator packet, BlockId blockId, Vector3Int position)
        {
            PlaceBlockWithWiring(packet, blockId, position, BlockPlacementWiring.AutoConnect);
        }

        protected static void PlaceBlockWithWiring(PacketResponseCreator packet, BlockId blockId, Vector3Int position, BlockPlacementWiring wiring)
        {
            var placeInfo = new List<PlaceInfo>
            {
                new()
                {
                    Position = position,
                    Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = blockId,
                },
            };

            var payload = MessagePackSerializer.Serialize(new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(placeInfo, wiring));
            packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
        }

        protected static int GetWireCount(IOpenableInventory inventory)
        {
            return GetItemCount(inventory, WireItemGuid);
        }

        protected static int GetItemCount(IOpenableInventory inventory, Guid itemGuid)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
            var total = 0;
            foreach (var itemStack in inventory.InventoryItems)
                if (itemStack.Id == itemId)
                    total += itemStack.Count;

            return total;
        }

        #endregion
    }
}

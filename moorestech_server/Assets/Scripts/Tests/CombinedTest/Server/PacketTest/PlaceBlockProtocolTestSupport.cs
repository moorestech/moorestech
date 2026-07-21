using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.UnlockState.States;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Construction;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// PlaceBlockProtocol系テストの共有ヘルパ（サーバー生成・素材操作・ペイロード生成）
    /// Shared helpers for PlaceBlockProtocol tests (server bootstrap, item ops, payload building)
    /// </summary>
    public static class PlaceBlockProtocolTestSupport
    {
        public const int PlayerId = 3;

        public static (PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        public static IOpenableInventory GetInventory(ServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
        }

        public static void SetItem(IOpenableInventory inventory, int slot, Guid itemGuid, int count)
        {
            inventory.SetItem(slot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(itemGuid), count));
        }

        public static int GetItemCount(IOpenableInventory inventory, Guid itemGuid)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
            var total = 0;
            foreach (var stack in inventory.InventoryItems)
            {
                if (stack.Id != itemId) continue;
                total += stack.Count;
            }
            return total;
        }

        public static byte[] CreatePlaceBlockPayload(BlockId blockId, params (int x, int y)[] positions)
        {
            var placeInfos = new List<PlaceInfo>();
            foreach (var (x, y) in positions)
            {
                placeInfos.Add(new PlaceInfo
                {
                    Position = new Vector3Int(x, y),
                    Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = blockId,
                });
            }
            return CreatePlacePayload(placeInfos);
        }

        public static byte[] CreatePlacePayload(List<PlaceInfo> placeInfos)
        {
            return MessagePackSerializer.Serialize(new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(PlayerId, placeInfos));
        }

        public static byte[] CreateReplacePayload(BlockId blockId, Vector3Int position, BlockDirection direction)
        {
            // リプレースフラグ付きの単一セルペイロードを生成する
            // Build a single-cell payload flagged for replace placement
            var placeInfos = new List<PlaceInfo>
            {
                new()
                {
                    Position = position,
                    Direction = direction,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = blockId,
                    IsReplace = true,
                },
            };
            return CreatePlacePayload(placeInfos);
        }

        public static void GrantRequiredItems(ServiceProvider serviceProvider, BlockId blockId, int costSets)
        {
            var inventory = GetInventory(serviceProvider);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var itemCounts = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            foreach (var (itemId, count) in itemCounts)
            {
                inventory.InsertItem(itemId, count * costSets);
            }
        }

        public static void AssertInventoryEmptyOfRequiredItems(ServiceProvider serviceProvider, BlockId blockId)
        {
            var inventory = GetInventory(serviceProvider);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            foreach (var requiredItem in blockMaster.RequiredItems)
            {
                Assert.AreEqual(0, GetItemCount(inventory, requiredItem.ItemGuid));
            }
        }

        public static void AssertRequiredItemsCount(ServiceProvider serviceProvider, BlockId blockId, int costSets)
        {
            // ブロックの必要素材が指定セット分だけインベントリに存在することを検証する
            // Assert the block's required items are present in the inventory for the given number of cost sets
            var inventory = GetInventory(serviceProvider);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            foreach (var requiredItem in blockMaster.RequiredItems)
            {
                Assert.AreEqual(requiredItem.Count * costSets, GetItemCount(inventory, requiredItem.ItemGuid));
            }
        }

        public static void OccupyAllInventorySlots(ServiceProvider serviceProvider, ItemId fillerItemId)
        {
            // 全スロットをコスト外アイテムで埋め、返却挿入の空きを無くす
            // Occupy every slot with a non-cost item so no room remains for refund insertion
            var inventory = GetInventory(serviceProvider);
            for (var i = 0; i < inventory.GetSlotSize(); i++)
            {
                inventory.SetItem(i, ServerContext.ItemStackFactory.Create(fillerItemId, 1));
            }
        }

        public static void UnlockBlock(ServiceProvider serviceProvider, BlockId blockId)
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlock(blockGuid);
        }

        public static void UnlockConnectTool(ServiceProvider serviceProvider, Guid connectToolGuid)
        {
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(connectToolGuid);
        }

        public static void LockBlock(ServiceProvider serviceProvider, BlockId blockId)
        {
            // IGameUnlockStateDataControllerにはUnlockのみ存在するため、Load経由で強制的にロック状態へ書き換える
            // The controller only exposes Unlock, so force the locked state back via a state-load overwrite
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();
            controller.LoadUnlockState(new GameUnlockStateJsonObject
            {
                BlockUnlockStateInfos = new List<BlockUnlockStateInfoJsonObject>
                {
                    new() { BlockGuid = blockGuid.ToString(), IsUnlocked = false },
                },
            });
        }
    }
}

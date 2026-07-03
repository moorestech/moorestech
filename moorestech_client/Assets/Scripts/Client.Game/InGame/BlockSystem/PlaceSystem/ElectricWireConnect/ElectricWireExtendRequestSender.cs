using System;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 電線ツールのプロトコル送信と延長用電柱アイテムの自動選択を担う
    /// Sends electric-wire tool protocols and auto-selects the pole item used for extension
    /// </summary>
    public static class ElectricWireExtendRequestSender
    {
        /// <summary>
        /// インベントリ内の最初の電柱ブロックアイテムをメインスロットから探す
        /// Find the first electric pole block item from the main inventory slots
        /// </summary>
        public static bool TryFindPoleSlot(ILocalPlayerInventory inventory, out int slot, out BlockMasterElement poleMaster, out ItemId poleItemId)
        {
            slot = -1;
            poleMaster = null;
            poleItemId = ItemMaster.EmptyItemId;

            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var itemId = inventory[i].Id;
                if (inventory[i].Count < 1 || !MasterHolder.BlockMaster.IsBlock(itemId)) continue;

                var master = MasterHolder.BlockMaster.GetBlockMaster(itemId);
                if (master.BlockParam is not ElectricPoleBlockParam) continue;

                slot = i;
                poleMaster = master;
                poleItemId = itemId;
                return true;
            }

            return false;
        }

        public static void Connect(Vector3Int fromPos, Vector3Int toPos, ItemId wireItemId)
        {
            ClientContext.VanillaApi.SendOnly.ConnectElectricWire(fromPos, toPos, wireItemId);
        }

        public static void Disconnect(Vector3Int posA, Vector3Int posB)
        {
            ClientContext.VanillaApi.SendOnly.DisconnectElectricWire(posA, posB);
        }

        /// <summary>
        /// 電柱を設置しつつ起点へ接続し、設置された電柱GameObjectを解決してコールバックする
        /// Place a pole while wiring it to the origin, then resolve the placed pole GameObject and call back
        /// </summary>
        public static void Extend(Vector3Int fromPos, int poleSlot, PlaceInfo polePlaceInfo, ItemId wireItemId, BlockGameObjectDataStore blockDataStore, Action<BlockGameObject> onPlaced)
        {
            UniTask.Create(async () =>
            {
                var response = await ClientContext.VanillaApi.Response.ExtendElectricWire(fromPos, poleSlot, polePlaceInfo, wireItemId, CancellationToken.None);
                if (response == null || !response.IsSuccess) return;

                // 設置イベントの反映を待ってから電柱GameObjectを解決する
                // Wait for the placement event to apply, then resolve the pole GameObject
                var placedId = new BlockInstanceId(response.PlacedBlockInstanceId);
                await UniTask.WhenAny(
                    UniTask.WaitForSeconds(1f),
                    UniTask.WaitUntil(() => blockDataStore.TryGetBlockGameObject(placedId, out _)));

                if (blockDataStore.TryGetBlockGameObject(placedId, out var placedBlock))
                {
                    onPlaced(placedBlock);
                }
            });
        }
    }
}

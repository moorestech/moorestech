using System;
using Client.Game.InGame.Context;
using Core.Inventory;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     アイテム付与・在庫確認の操作群。Direct（サーバー直挿入）とViaCommand（本番givePath）の2経路を提供
    ///     Item grant/inventory helpers with two paths: Direct (server insert) and ViaCommand (production give path)
    /// </summary>
    public static class PlaytestItemOps
    {
        public static ItemId ResolveItemId(string itemName)
        {
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                if (MasterHolder.ItemMaster.GetItemMaster(itemId).Name == itemName) return itemId;
            }
            throw new ArgumentException($"Item not found: {itemName}");
        }

        public static void GiveItemDirect(string itemName, int count)
        {
            // サーバーのインベントリへ同期的に直接挿入する（待機不要・状態を素早く作る用）
            // Insert synchronously into the server inventory (no wait; for fast state setup)
            var itemId = ResolveItemId(itemName);
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            var itemStack = ServerContext.ItemStackFactory.Create(itemId, count);
            GetMainInventory(playerId).InsertItem(itemStack);
        }

        public static async UniTask GiveItemViaCommand(string itemName, int count, float timeoutSeconds)
        {
            var itemId = ResolveItemId(itemName);
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            var beforeCount = CountItem(playerId, itemId);

            // 本番のgiveコマンド経路で付与し、サーバー在庫への反映を条件待機する
            // Grant via the production give-command path and poll until the server inventory reflects it
            var command = $"{SendCommandProtocol.GiveCommand} {playerId} {itemId.AsPrimitive()} {count}";
            ClientContext.VanillaApi.SendOnly.SendCommand(command);

            var startTime = Time.realtimeSinceStartup;
            while (CountItem(playerId, itemId) < beforeCount + count)
            {
                if (Time.realtimeSinceStartup - startTime > timeoutSeconds)
                {
                    throw new TimeoutException($"give '{itemName}' x{count} not reflected within {timeoutSeconds}s");
                }
                await UniTask.Yield();
            }
        }

        public static int CountItem(int playerId, ItemId itemId)
        {
            // メインインベントリ内の対象アイテム総数を数える
            // Count the total amount of the target item in the main inventory
            var total = 0;
            foreach (var stack in GetMainInventory(playerId).InventoryItems)
            {
                if (stack.Id == itemId) total += stack.Count;
            }
            return total;
        }

        private static IOpenableInventory GetMainInventory(int playerId)
        {
            return ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
        }
    }
}

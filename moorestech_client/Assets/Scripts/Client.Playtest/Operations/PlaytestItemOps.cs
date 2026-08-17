using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Inventory;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;
using VContainer;

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
                if (timeoutSeconds < Time.realtimeSinceStartup - startTime)
                {
                    throw new TimeoutException($"give '{itemName}' x{count} not reflected within {timeoutSeconds}s");
                }
                await UniTask.Yield();
            }
        }

        public static async UniTask EquipItem(string itemName, int equipmentSlot, float timeoutSeconds)
        {
            // 持ち物から装備枠へ移して選択する。採掘はサーバー権威で選択中装備を見るため、両方揃って初めて成立する
            // Move the stack from the inventory into an equipment slot and select it; server-authoritative mining reads the selected equipment, so both steps are required
            var itemId = ResolveItemId(itemName);
            var resolver = ClientDIContext.DIContainer.DIContainerResolver;
            var localInventory = resolver.Resolve<ILocalPlayerInventory>();
            var startTime = Time.realtimeSinceStartup;

            // クラフト直後などはクライアントミラーへの反映が遅れるため、移動元が現れるまで待つ
            // The client mirror lags right after a craft, so wait until the source stack shows up
            while (FindClientSlot() < 0)
            {
                ThrowIfTimeout($"'{itemName}' never appeared in the client inventory");
                await UniTask.Yield();
            }

            var sourceSlot = FindClientSlot();
            resolver.Resolve<LocalPlayerInventoryController>().MoveItem(LocalMoveInventoryType.MainOrSub, sourceSlot, LocalMoveInventoryType.Equipment, equipmentSlot, localInventory[sourceSlot].Count);
            resolver.Resolve<LocalPlayerEquipment>().SetSelectedIndex(equipmentSlot);

            // サーバーの装備インベントリへ反映されるまで待つ（採掘判定が読むのはこちらのため）
            // Wait until the server equipment inventory reflects it, since the mining check reads that side
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            var equipmentInventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).EquipmentInventory;
            while (equipmentInventory.GetSelectedItem().Id != itemId)
            {
                ThrowIfTimeout($"equip '{itemName}' into slot {equipmentSlot} did not reach the server");
                await UniTask.Yield();
            }

            #region Internal

            int FindClientSlot()
            {
                for (var slot = 0; slot < localInventory.Count; slot++)
                {
                    if (localInventory[slot].Id == itemId && 0 < localInventory[slot].Count) return slot;
                }
                return -1;
            }

            void ThrowIfTimeout(string message)
            {
                if (timeoutSeconds < Time.realtimeSinceStartup - startTime) throw new TimeoutException($"{message} within {timeoutSeconds}s");
            }

            #endregion
        }

        public static async UniTask GiveConstructionCost(string blockName, int blockCount, float timeoutSeconds)
        {
            // ブロックマスタのRequiredItemsをブロック数分付与する（UI設置はインベントリからコストを消費する）
            // Grant the block master's RequiredItems for the given block count (UI placement consumes inventory cost)
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(PlaytestBlockOps.ResolveBlockId(blockName));
            if (blockMaster.RequiredItems == null || blockMaster.RequiredItems.Length == 0) return;

            foreach (var required in blockMaster.RequiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(required.ItemGuid);
                var itemName = MasterHolder.ItemMaster.GetItemMaster(itemId).Name;
                var giveCount = required.Count * blockCount;
                var clientCountBefore = CountItemClientSide(itemId);

                await GiveItemViaCommand(itemName, giveCount, timeoutSeconds);

                // 設置コスト判定はクライアント側インベントリを見るため、イベント同期の反映まで待つ
                // Placement cost checks read the client-side inventory, so wait until the event sync lands
                await WaitClientItemCount(itemId, clientCountBefore + giveCount, timeoutSeconds);
            }
        }

        public static int CountItemClientSide(ItemId itemId)
        {
            var localInventory = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ILocalPlayerInventory>();
            var total = 0;
            foreach (var stack in localInventory)
            {
                if (stack.Id == itemId) total += stack.Count;
            }
            return total;
        }

        public static async UniTask WaitClientItemCount(ItemId itemId, int expectedMinimum, float timeoutSeconds)
        {
            var startTime = Time.realtimeSinceStartup;
            while (CountItemClientSide(itemId) < expectedMinimum)
            {
                if (timeoutSeconds < Time.realtimeSinceStartup - startTime)
                {
                    throw new TimeoutException($"client inventory of item {itemId} did not reach {expectedMinimum} within {timeoutSeconds}s");
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

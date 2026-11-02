using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Interface;
using Game.Context;
using UnityEngine;

namespace Game.Block.Blocks.SpaceElevator
{
    // 縮小したスロットへのロードは、何も確定する前に残存品を検出する
    // Detect retained items in truncated slots before committing any loaded inventory
    internal static class SpaceElevatorInventoryRestorer
    {
        internal static void Restore(IReadOnlyList<ItemStackSaveJsonObject> items, OpenableInventoryItemDataStoreService inventory, int slotCount, BlockInstanceId instanceId)
        {
            for (var i = slotCount; i < items.Count; i++)
            {
                // silent-ok: 空の末尾には保存すべき品物が無い / 残る在庫はロード後のUIで見える
                if (items[i].Count == 0) continue;
                var reason = $"[SpaceElevatorBase] 出口縮小で非空スロット {i} を失うためロードを中断します BlockInstanceId:{instanceId} count:{items[i].Count}";
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }

            // 全品が収まることを確かめてから復元する
            // Restore only after verifying every saved item can be retained
            for (var i = 0; i < slotCount && i < items.Count; i++) inventory.SetItemWithoutEvent(i, items[i].ToItemStack());
        }
    }
}

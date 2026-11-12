using System.Collections.Generic;
using Client.Ui.State.Items;
using UnityEngine;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// ブロックの枠の番地から中身を読む 1 か所（BlockItemGrid と機械の帯が共有する）
    /// The single place that reads a block slot by index (shared by BlockItemGrid and the machine bands)
    /// </summary>
    public static class BlockSlotReader
    {
        // 番地が枠数を超えるのは呼び手（区画）の誤り。購読を例外で止めず空きで描き、LogError で知らせる
        // An index beyond the slot count is the caller's (section's) bug; draw it empty instead of killing the subscription, and LogError
        public static SlotItem Read(IReadOnlyList<SlotItem> itemSlots, int index, string ownerTag, string ownerId)
        {
            if (0 <= index && index < itemSlots.Count) return itemSlots[index];
            Debug.LogError($"[{ownerTag}] {ownerId}: slot index {index} is out of range (slot count {itemSlots.Count}); drawn as empty");
            return default;
        }
    }
}

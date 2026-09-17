using System.Linq;
using Game.SaveLoad.Pruning.Items;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning.Sections
{
    /// <summary>playerInventory節: プレイヤーごとのメイン・掴み・装備スロットの在庫スタックを空にする</summary>
    /// <summary>The playerInventory section: empties the stacks in each player's main, grab and equipment slots</summary>
    /// <summary>綴りはPlayerInventorySaveJsonObjectのJsonPropertyに一致させる</summary>
    /// <summary>Spellings match the JsonProperty names of PlayerInventorySaveJsonObject</summary>
    public sealed class PlayerInventorySectionPruner : IMissingMasterSectionPruner
    {
        private const string PlayerIdKey = "PlayerId";
        private const string MainInventoryKey = "MainInventoryItems";
        private const string GrabInventoryKey = "GrabInventoryItems";
        private const string EquipmentInventoryKey = "EquipmentInventoryItems";

        public string SaveSectionName => "playerInventory";

        public MissingMasterSectionPruneResult Prune(JToken section)
        {
            var result = new MissingMasterSectionPruneResult();
            if (section is not JArray players)
            {
                Debug.Log($"playerInventory節が配列でないためプレイヤー所持品の除去を行いません。 type={section.Type}");
                return result;
            }

            var cleaner = new MissingItemReferenceCleaner(result);
            foreach (var player in players.OfType<JObject>())
            {
                var playerId = (int?)player[PlayerIdKey];
                EmptySlotList(player, playerId, MainInventoryKey);
                EmptyGrabSlot(player, playerId);
                EmptySlotList(player, playerId, EquipmentInventoryKey);
            }

            cleaner.LogRemovedItemReferences(SaveSectionName);
            return result;

            #region Internal

            // スロット番号は配列の添字。PlayerInventorySaveJsonObjectがGetSlotSize順に並べて保存する
            // The slot number is the array index; PlayerInventorySaveJsonObject saves slots in GetSlotSize order
            void EmptySlotList(JObject player, int? playerId, string containerKey)
            {
                if (player[containerKey] is not JArray slots)
                {
                    Debug.LogWarning($"プレイヤー所持品の{containerKey}が配列でないため除去を行いません。 playerId={playerId} type={player[containerKey]?.Type}");
                    return;
                }

                for (var slot = 0; slot < slots.Count; slot++)
                {
                    if (slots[slot] is not JObject stack) continue;
                    cleaner.EmptyItemStackIfMissing(stack, PrunedItemOrigin.PlayerInventorySlot(SaveSectionName, playerId, containerKey, slot));
                }
            }

            // 掴みインベントリは1枠だけなのでスロット0として控える
            // The grab inventory has a single slot, recorded as slot 0
            void EmptyGrabSlot(JObject player, int? playerId)
            {
                if (player[GrabInventoryKey] is not JObject grabStack)
                {
                    Debug.LogWarning($"プレイヤー所持品の{GrabInventoryKey}がオブジェクトでないため除去を行いません。 playerId={playerId} type={player[GrabInventoryKey]?.Type}");
                    return;
                }

                cleaner.EmptyItemStackIfMissing(grabStack, PrunedItemOrigin.PlayerInventorySlot(SaveSectionName, playerId, GrabInventoryKey, 0));
            }

            #endregion
        }
    }
}

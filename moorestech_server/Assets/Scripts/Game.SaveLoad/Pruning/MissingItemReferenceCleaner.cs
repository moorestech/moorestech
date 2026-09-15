using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning
{
    /// <summary>見つかったアイテム参照がマスタに無ければ空にし、元の値を控える。どこを見るかは走査側の責務</summary>
    /// <summary>Empties an item reference whose item is absent from the master and keeps the original; where to look is the walker's job</summary>
    public sealed class MissingItemReferenceCleaner
    {
        private const int LoggedItemGuidLimit = 10;

        private readonly Dictionary<string, int> _countByItemGuid = new();
        private readonly JArray _emptiedItemStacks = new();
        private readonly JArray _neutralizedConnectionMaterials = new();

        // 埋め込みJSONを書き戻すかの判定に使う。控えた総数が増えたときだけ書き戻す
        // Used to decide whether embedded JSON is written back: only when this total has grown
        public int RemovalCount { get; private set; }

        public void EmptyItemStackIfMissing(JObject stack)
        {
            if (stack[SaveItemReferenceFields.ItemStackItemGuidKey] is not JValue guidValue) return;

            var guidText = guidValue.Value<string>();
            if (!IsMissingItem(guidText)) return;

            Record(_emptiedItemStacks, stack.DeepClone(), guidText);
            EmptyItemStackInPlace(stack);
        }

        // 接続コスト素材は在庫ではないので件数も除去データも分ける。ただし空にしないとロードで例外になる
        // Connection materials are not inventory so counts and archives stay apart, yet they must be emptied or the load throws
        public void NeutralizeConnectionMaterialsIfMissing(JToken materials)
        {
            if (materials is not JArray materialArray) return;

            foreach (var material in materialArray.OfType<JObject>().ToList())
            {
                if (material[SaveItemReferenceFields.ItemStackItemGuidKey] is not JValue guidValue) continue;

                var guidText = guidValue.Value<string>();
                if (!IsMissingItem(guidText)) continue;

                Record(_neutralizedConnectionMaterials, material.DeepClone(), guidText);
                EmptyItemStackInPlace(material);
            }
        }

        // 裸guidは空guidへ戻さずnullへ落とす。復元側がguidとして読めない値を「燃料なし」として扱う
        // A bare guid is dropped to null rather than the empty guid; the restore treats an unreadable value as "no fuel"
        public void ClearBareItemGuidIfMissing(JProperty property)
        {
            if (property.Value is not JValue guidValue) return;

            // 燃料切れ等で元々空欄の値は除去対象でも壊れた値でもないので、警告を出さずに抜ける
            // A value blank from the start (e.g. out of fuel) is neither missing nor corrupt, so leave without a warning
            var guidText = guidValue.Value<string>();
            if (guidValue.Type == JTokenType.Null || string.IsNullOrEmpty(guidText)) return;
            if (!IsMissingItem(guidText)) return;

            // countは在庫スタックと同じキーで揃える。裸guidは1件そのものを指すので1固定（在庫のcountとは意味が違うが後日の返金入力として形を合わせる）
            // count mirrors the inventory stack key; a bare guid always denotes exactly one item, so it is fixed at 1 (a different meaning than stack counts, but shape-matched for the later refund input)
            Record(_emptiedItemStacks, new JObject
            {
                ["field"] = property.Name,
                [SaveItemReferenceFields.ItemStackItemGuidKey] = guidText,
                [SaveItemReferenceFields.ItemStackCountKey] = 1,
            }, guidText);
            property.Value = JValue.CreateNull();
        }

        // modを外すと数千件になるのでguidごとに集約して1行にまとめる
        // Dropping a mod can empty thousands of references, so the report is aggregated per guid into one line
        public void LogRemovedItemReferences()
        {
            if (RemovalCount == 0) return;

            var digest = string.Join(", ", _countByItemGuid.Take(LoggedItemGuidLimit).Select(pair => $"{pair.Key}x{pair.Value}"));
            Debug.LogWarning($"マスタに存在しないアイテム参照を空にしました。 items={_emptiedItemStacks.Count} connectionMaterials={_neutralizedConnectionMaterials.Count} guidKinds={_countByItemGuid.Count} detail={digest}");
        }

        public ItemPruneWalkResult ToResult()
        {
            return new ItemPruneWalkResult(_emptiedItemStacks, _neutralizedConnectionMaterials);
        }

        private void Record(JArray removed, JToken original, string itemGuidText)
        {
            removed.Add(original);
            _countByItemGuid[itemGuidText] = _countByItemGuid.GetValueOrDefault(itemGuidText) + 1;
            RemovalCount++;
        }

        private static void EmptyItemStackInPlace(JObject stack)
        {
            stack[SaveItemReferenceFields.ItemStackItemGuidKey] = Guid.Empty.ToString();
            // countを持たない別形のJSONへ新しいキーを生やさない
            // Never grow a count key on a differently shaped JSON that did not have one
            if (stack[SaveItemReferenceFields.ItemStackCountKey] != null) stack[SaveItemReferenceFields.ItemStackCountKey] = 0;
        }

        // guidとして読めない値は壊れているかアイテム参照でない。残す判断の理由を出す
        // A value that is no guid is corrupt or not an item reference at all; log why it is left alone
        private static bool IsMissingItem(string itemGuidText)
        {
            if (!Guid.TryParse(itemGuidText, out var guid))
            {
                Debug.LogWarning($"アイテムguidとして読めないため除去判定せず残します。 itemGuid={itemGuidText}");
                return false;
            }

            if (guid == Guid.Empty) return false;
            return !MasterHolder.ItemMaster.ExistItemId(guid);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning.Items
{
    /// <summary>見つかったアイテム参照がマスタに無ければ空にし、元の値を住所つきで控える。どこを見るかは呼び出し側の責務</summary>
    /// <summary>Empties an item reference absent from the master and records the original with its address; where to look is the caller's job</summary>
    public sealed class MissingItemReferenceCleaner
    {
        private const int LoggedItemGuidLimit = 10;

        // 退避JSONのkind。在庫スタックは個数を持ち、裸guidは個数を持たない（捏造しない）
        // The kind in the pruned JSON; an inventory stack carries a count while a bare guid has none (never fabricated)
        private const string ItemStackKind = "itemStack";
        private const string BareGuidKind = "bareGuid";

        private readonly MissingMasterSectionPruneResult _result;
        private readonly Dictionary<string, int> _countByItemGuid = new();

        // 埋め込みJSONを書き戻すかの判定に使う。控えた総数が増えたときだけ書き戻す
        // Used to decide whether embedded JSON is written back: only when this total has grown
        public int RemovalCount { get; private set; }

        public MissingItemReferenceCleaner(MissingMasterSectionPruneResult result)
        {
            _result = result;
        }

        public void EmptyItemStackIfMissing(JObject stack, PrunedItemOrigin origin)
        {
            if (!TryReadMissingItemGuid(stack[ItemStackJsonKeys.ItemGuid], out var guidText)) return;

            Record(_result.EmptiedItemStacks, StackEntry(stack, guidText, origin), guidText);
            EmptyItemStackInPlace(stack);
        }

        // 接続コスト素材は在庫ではないので件数も除去データも分ける。ただし空にしないとロードで例外になる
        // Connection materials are not inventory so counts and archives stay apart, yet they must be emptied or the load throws
        public void NeutralizeConnectionMaterialIfMissing(JObject material, PrunedItemOrigin origin)
        {
            if (!TryReadMissingItemGuid(material[ItemStackJsonKeys.ItemGuid], out var guidText)) return;

            Record(_result.NeutralizedConnectionMaterials, StackEntry(material, guidText, origin), guidText);
            EmptyItemStackInPlace(material);
        }

        // 裸guidは空guidへ戻さずnullへ落とす。復元側がguidとして読めない値を「燃料なし」として扱う
        // A bare guid is dropped to null rather than the empty guid; the restore treats an unreadable value as "no fuel"
        public void ClearBareItemGuidIfMissing(JProperty property, PrunedItemOrigin origin)
        {
            if (property.Value is not JValue guidValue) return;

            // 燃料切れ等で元々空欄の値は除去対象でも壊れた値でもないので、警告を出さずに抜ける
            // A value blank from the start (e.g. out of fuel) is neither missing nor corrupt, so leave without a warning
            if (guidValue.Type == JTokenType.Null || string.IsNullOrEmpty(guidValue.Value<string>())) return;
            if (!TryReadMissingItemGuid(guidValue, out var guidText)) return;

            Record(_result.EmptiedItemStacks, new JObject
            {
                ["origin"] = origin.WithField(property.Name).ToJson(),
                ["kind"] = BareGuidKind,
                [ItemStackJsonKeys.ItemGuid] = guidText,
            }, guidText);
            property.Value = JValue.CreateNull();
        }

        // modを外すと数千件になるのでguidごとに集約して1行にまとめる
        // Dropping a mod can empty thousands of references, so the report is aggregated per guid into one line
        public void LogRemovedItemReferences(string sectionName)
        {
            if (RemovalCount == 0) return;

            var digest = string.Join(", ", _countByItemGuid.Take(LoggedItemGuidLimit).Select(pair => $"{pair.Key}x{pair.Value}"));
            Debug.LogWarning($"マスタに存在しないアイテム参照を空にしました。 section={sectionName} items={_result.EmptiedItemStacks.Count} connectionMaterials={_result.NeutralizedConnectionMaterials.Count} guidKinds={_countByItemGuid.Count} detail={digest}");
        }

        private void Record(JArray removed, JObject entry, string itemGuidText)
        {
            removed.Add(entry);
            _countByItemGuid[itemGuidText] = _countByItemGuid.GetValueOrDefault(itemGuidText) + 1;
            RemovalCount++;
        }

        private static JObject StackEntry(JObject stack, string guidText, PrunedItemOrigin origin)
        {
            var entry = new JObject
            {
                ["origin"] = origin.ToJson(),
                ["kind"] = ItemStackKind,
                [ItemStackJsonKeys.ItemGuid] = guidText,
            };
            // 個数を持たない形のスタックへは個数を捏造しない
            // Never fabricate a count for a stack shape that had none
            if (stack[ItemStackJsonKeys.Count] != null) entry[ItemStackJsonKeys.Count] = stack[ItemStackJsonKeys.Count].DeepClone();
            return entry;
        }

        private static void EmptyItemStackInPlace(JObject stack)
        {
            stack[ItemStackJsonKeys.ItemGuid] = Guid.Empty.ToString();
            // countを持たない別形のJSONへ新しいキーを生やさない
            // Never grow a count key on a differently shaped JSON that did not have one
            if (stack[ItemStackJsonKeys.Count] != null) stack[ItemStackJsonKeys.Count] = 0;
        }

        // guidとして読めない値は壊れているかアイテム参照でない。残す判断の理由を出す
        // A value that is no guid is corrupt or not an item reference at all; log why it is left alone
        private static bool TryReadMissingItemGuid(JToken guidToken, out string guidText)
        {
            guidText = null;
            if (guidToken is not JValue guidValue) return false;

            guidText = guidValue.Value<string>();
            if (!Guid.TryParse(guidText, out var guid))
            {
                Debug.LogWarning($"アイテムguidとして読めないため除去判定せず残します。 itemGuid={guidText}");
                return false;
            }

            if (guid == Guid.Empty) return false;
            return !MasterHolder.ItemMaster.ExistItemId(guid);
        }
    }
}

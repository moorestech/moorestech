using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    /// <summary>
    /// 旧マスタでコスト0のまま完了できたTier9研究の完了を外し、その研究が解放した記録を消す（冪等）
    /// Remove completions of Tier9 research that old masters let players finish at zero cost, and delete the unlock records they produced (idempotent)
    /// </summary>
    /// <remarks>
    /// 解放記録はfalseにせず消す。記録が無ければロード時にマスタの初期値が使われ、残った研究とチャレンジの解放やり直しが必要な分を解放し直す。
    /// Unlock records are deleted rather than set to false: without a record the master's initial value applies on load, and the unlock replays of the remaining research and challenges re-grant whatever is still owed.
    /// </remarks>
    public sealed class ZeroCostTier9ResearchResetter : IV7ToV8MigrationPart
    {
        // ResearchSaveJsonObjectはJsonProperty無しなのでプロパティ名がそのままキーになる
        // ResearchSaveJsonObject carries no JsonProperty, so the property name itself is the key
        private const string ResearchKey = "research";
        private const string CompletedResearchGuidsKey = "CompletedResearchGuids";
        private const string GameUnlockStateKey = "gameUnlockState";
        private const string ItemUnlockStateInfosKey = "itemUnlockStateInfos";
        private const string BlockUnlockStateInfosKey = "blockUnlockStateInfos";
        private const string GuidKey = "guid";

        private readonly List<JToken> _completedEntries = new();
        private readonly HashSet<Guid> _resetResearchGuids = new();
        private readonly List<JToken> _unlockRecords = new();

        public bool TryPrepare(JObject save, out string failureReason)
        {
            // 完了一覧から対象ノードを集める。研究節か完了一覧が無い・null のセーブは外すものが無い
            // Collect target nodes from the completed list; a save whose research section or list is absent or null has nothing to remove
            if (!V7ToV8SaveJson.TryGetNestedArray(save, ResearchKey, CompletedResearchGuidsKey, out var completed, out failureReason)) return false;
            if (!(completed is PresentNestedSaveArray presentCompleted)) return true;
            foreach (var entry in presentCompleted.Array)
            {
                // 読めない値は後段の刈り込みが警告して残すので、ここでは対象ノードだけを拾う
                // The later pruner warns about unreadable values, so only target nodes are picked here
                if (!TryReadGuid(entry, out var researchGuid)) continue;
                if (!ZeroCostTier9ResearchResetTable.UnlocksByResearch.ContainsKey(researchGuid)) continue;
                _completedEntries.Add(entry);
                _resetResearchGuids.Add(researchGuid);
            }
            if (_resetResearchGuids.Count == 0) return true;

            // 外すノードが旧マスタで解放したアイテム・ブロックの記録を集める
            // Collect the item and block records that the removed nodes unlocked in old masters
            var revokedItems = new HashSet<Guid>();
            var revokedBlocks = new HashSet<Guid>();
            foreach (var researchGuid in _resetResearchGuids)
            {
                var unlocks = ZeroCostTier9ResearchResetTable.UnlocksByResearch[researchGuid];
                revokedItems.UnionWith(unlocks.ItemGuids);
                revokedBlocks.UnionWith(unlocks.BlockGuids);
            }
            if (!TryCollectRecords(save, ItemUnlockStateInfosKey, revokedItems, out failureReason)) return false;
            return TryCollectRecords(save, BlockUnlockStateInfosKey, revokedBlocks, out failureReason);
        }

        public string ApplyAndSummarize()
        {
            foreach (var entry in _completedEntries) entry.Remove();
            foreach (var record in _unlockRecords) record.Remove();
            return $"コスト0研究のリセット: 完了を外した研究={_resetResearchGuids.Count}件[{string.Join(",", _resetResearchGuids)}] 消した解放記録={_unlockRecords.Count}件";
        }

        // 解放状態の配列から対象GUIDの記録を集める。解放状態の節か配列が無い・null のセーブは消すものが無い
        // Collect records of target GUIDs from an unlock-state array; a save whose section or array is absent or null has nothing to delete
        private bool TryCollectRecords(JObject save, string infosKey, HashSet<Guid> targetGuids, out string failureReason)
        {
            if (!V7ToV8SaveJson.TryGetNestedArray(save, GameUnlockStateKey, infosKey, out var infos, out failureReason)) return false;
            if (!(infos is PresentNestedSaveArray presentInfos)) return true;
            foreach (var infoToken in presentInfos.Array)
            {
                if (!(infoToken is JObject info)) return V7ToV8SaveJson.Reject($"{GameUnlockStateKey}.{infosKey} の要素がオブジェクトではありません。 type={infoToken.Type}", out failureReason);
                if (TryReadGuid(info[GuidKey], out var guid) && targetGuids.Contains(guid)) _unlockRecords.Add(info);
            }

            return true;
        }

        private static bool TryReadGuid(JToken token, out Guid guid)
        {
            guid = Guid.Empty;
            return token is JValue { Type: JTokenType.String } value && Guid.TryParse((string)value, out guid);
        }
    }
}

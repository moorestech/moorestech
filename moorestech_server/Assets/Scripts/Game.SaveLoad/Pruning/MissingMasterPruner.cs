using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning
{
    /// <summary>
    /// マスタから消えたブロック・アイテム・研究ノードをロード前のセーブJSONから取り除く
    /// Removes blocks, items and research nodes that vanished from the master out of the save JSON before load
    /// mapObjectは対象外。マップ側に無いinstanceIdをMapObjectDatastore.LoadMapObjectが既にスキップする
    /// Map objects are out of scope; MapObjectDatastore.LoadMapObject already skips instance ids absent from the map
    /// </summary>
    public sealed class MissingMasterPruner
    {
        private const string ItemGuidKey = "itemGuid";
        private const string CountKey = "count";

        public MissingMasterPruneOutcome Prune(JObject save)
        {
            var removedBlocks = PruneBlocks();
            var removedItemStacks = PruneItemStacks();
            var removedResearchGuids = PruneResearch();

            return new MissingMasterPruneOutcome(save, removedBlocks, removedItemStacks, removedResearchGuids);

            #region Internal

            JArray PruneBlocks()
            {
                var removed = new JArray();
                if (save["world"] is not JArray world) return removed;

                foreach (var block in world.OfType<JObject>().ToList())
                {
                    var guidText = block["blockGuid"]?.Value<string>();
                    if (!Guid.TryParse(guidText, out var guid)) continue;
                    if (MasterHolder.BlockMaster.GetBlockIdOrNull(guid) != null) continue;

                    Debug.LogWarning($"マスタに存在しないブロックをセーブから除去します。 blockGuid={guidText} instanceId={block["instanceId"]}");
                    removed.Add(block.DeepClone());
                    block.Remove();
                }

                return removed;
            }

            JArray PruneItemStacks()
            {
                var removed = new JArray();
                // ブロック除去後の木を丸ごと歩く。プレイヤー・チェスト・機械のどこにスタックがあっても拾う
                // Walk the whole tree after block removal so stacks are caught wherever they sit
                WalkForItemStacks(save, removed);
                return removed;
            }

            JArray PruneResearch()
            {
                var removed = new JArray();
                if (save["research"]?["CompletedResearchGuids"] is not JArray completed) return removed;

                foreach (var entry in completed.ToList())
                {
                    var guidText = entry.Value<string>();
                    if (!Guid.TryParse(guidText, out var guid)) continue;
                    if (MasterHolder.ResearchMaster.GetResearch(guid) != null) continue;

                    Debug.LogWarning($"マスタに存在しない研究ノードを完了一覧から除去します。 researchGuid={guidText}");
                    removed.Add(guidText);
                    entry.Remove();
                }

                return removed;
            }

            #endregion
        }

        // itemGuidを持つJObjectを空スタックへ落とし、文字列に埋め込まれたJSONの中まで降りる
        // Empties every JObject carrying an itemGuid, descending into JSON embedded inside strings
        private void WalkForItemStacks(JToken token, JArray removed)
        {
            if (token is JArray array)
            {
                foreach (var child in array.ToList()) WalkForItemStacks(child, removed);
                return;
            }

            if (token is JValue value)
            {
                RewriteEmbeddedJson(value, removed);
                return;
            }

            if (token is not JObject json) return;

            if (json[ItemGuidKey] is JValue guidValue && TryFindMissingItem(guidValue, out var guidText))
            {
                Debug.LogWarning($"マスタに存在しないアイテムを空スタックへ落とします。 itemGuid={guidText} count={json[CountKey]}");
                removed.Add(json.DeepClone());
                json[ItemGuidKey] = Guid.Empty.ToString();
                json[CountKey] = 0;
            }

            foreach (var property in json.Properties().ToList()) WalkForItemStacks(property.Value, removed);
        }

        private bool TryFindMissingItem(JValue guidValue, out string guidText)
        {
            guidText = guidValue.Value<string>();
            if (!Guid.TryParse(guidText, out var guid)) return false;
            if (guid == Guid.Empty) return false;
            return !MasterHolder.ItemMaster.ExistItemId(guid);
        }

        // ブロックのstateはJSON文字列の入れ子。読めた場合だけ中身を除去して書き戻す
        // Block state nests JSON inside a string; only parsable values are pruned and written back
        private void RewriteEmbeddedJson(JValue value, JArray removed)
        {
            if (value.Type != JTokenType.String) return;
            var text = value.Value<string>();
            if (string.IsNullOrEmpty(text)) return;

            var trimmed = text.TrimStart();
            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("[")) return;

            JToken embedded;
            // 外部入力(セーブファイル)のパースなので隔離目的のtry-catchを使う。base64-MessagePack等は素通しする
            // Parsing external input (the save file), so the isolation try-catch is allowed; base64 MessagePack passes through
            try
            {
                embedded = JToken.Parse(text);
            }
            catch (JsonReaderException e)
            {
                Debug.Log($"JSONとして読めない状態値はアイテム除去の対象外です。 reason={e.Message}");
                return;
            }

            var before = removed.Count;
            WalkForItemStacks(embedded, removed);
            if (removed.Count == before) return;

            value.Value = embedded.ToString(Formatting.None);
        }
    }
}

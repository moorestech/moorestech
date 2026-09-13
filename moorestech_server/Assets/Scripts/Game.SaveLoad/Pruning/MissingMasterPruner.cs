using System;
using System.Linq;
using Core.Master;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning
{
    /// <summary>
    /// マスタから消えたブロック・アイテム・研究ノードをロード前のセーブJSONから取り除く
    /// Removes blocks, items and research nodes that vanished from the master out of the save JSON before load
    /// 渡したJObjectをその場で書き換え、同じインスタンスをOutcome.Saveとして返す
    /// The given JObject is rewritten in place and the very same instance comes back as Outcome.Save
    /// mapObjectは対象外。マップ側に無いinstanceIdをMapObjectDatastore.LoadMapObjectが既にスキップする
    /// Map objects are out of scope; MapObjectDatastore.LoadMapObject already skips instance ids absent from the map
    /// アイテム参照として何を見る・見ないかはSaveItemReferenceFieldsが持つ
    /// SaveItemReferenceFields holds what does and does not count as an item reference
    /// </summary>
    public sealed class MissingMasterPruner
    {
        public MissingMasterPruneOutcome Prune(JObject save)
        {
            var removedBlocks = PruneBlocks();
            var removedItemReferences = PruneItemReferences();
            var removedResearchGuids = PruneResearch();

            return new MissingMasterPruneOutcome(save, removedBlocks, removedItemReferences, removedResearchGuids);

            #region Internal

            JArray PruneBlocks()
            {
                var removed = new JArray();
                // world節が無い・配列でないセーブは除去できない。素通しを黙認すると不発に気づけない
                // A save without a world array cannot be pruned, and a silent pass would hide the no-op
                if (save["world"] is not JArray world)
                {
                    Debug.Log($"world節が配列でないためブロックの除去を行いません。 type={save["world"]?.Type}");
                    return removed;
                }

                foreach (var block in world.OfType<JObject>().ToList())
                {
                    // guidが読めないブロックは除去判定できないので残す。ロードで落ちうるため理由を残す
                    // A block with an unreadable guid cannot be judged so it stays; load may still throw, hence the log
                    var guidText = block["blockGuid"]?.Value<string>();
                    if (!Guid.TryParse(guidText, out var guid))
                    {
                        Debug.LogWarning($"blockGuidがguidとして読めないため除去判定せず残します。 blockGuid={guidText} instanceId={block["instanceId"]}");
                        continue;
                    }

                    if (MasterHolder.BlockMaster.GetBlockIdOrNull(guid) != null) continue;

                    Debug.LogWarning($"マスタに存在しないブロックをセーブから除去します。 blockGuid={guidText} instanceId={block["instanceId"]}");
                    removed.Add(block.DeepClone());
                    block.Remove();
                }

                return removed;
            }

            ItemPruneWalkResult PruneItemReferences()
            {
                // ブロック除去後の木を丸ごと歩く。プレイヤー・チェスト・機械のどこに参照があっても拾う
                // Walk the whole tree after block removal so references are caught wherever they sit
                return new ItemStackPruneWalker().Walk(save);
            }

            JArray PruneResearch()
            {
                var removed = new JArray();
                // 研究節が無いセーブもありうるが、除去が不発だった事実は読めるようにしておく
                // A save can legitimately lack the research node, but the no-op still has to leave a trace
                if (save["research"]?["CompletedResearchGuids"] is not JArray completed)
                {
                    Debug.Log("research.CompletedResearchGuidsが配列でないため研究ノードの除去を行いません。");
                    return removed;
                }

                foreach (var entry in completed.ToList())
                {
                    // 読めないguidは完了扱いのまま残る。研究解放の判定に響くので警告する
                    // An unreadable guid stays marked complete, which skews unlock checks, so warn
                    var guidText = entry.Value<string>();
                    if (!Guid.TryParse(guidText, out var guid))
                    {
                        Debug.LogWarning($"研究guidがguidとして読めないため除去判定せず残します。 researchGuid={guidText}");
                        continue;
                    }

                    if (MasterHolder.ResearchMaster.GetResearch(guid) != null) continue;

                    Debug.LogWarning($"マスタに存在しない研究ノードを完了一覧から除去します。 researchGuid={guidText}");
                    removed.Add(guidText);
                    entry.Remove();
                }

                return removed;
            }

            #endregion
        }
    }
}

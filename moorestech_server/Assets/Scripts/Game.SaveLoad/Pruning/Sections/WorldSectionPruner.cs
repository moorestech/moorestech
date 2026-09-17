using System;
using System.Linq;
using Core.Master;
using Game.SaveLoad.Pruning.Items;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning.Sections
{
    /// <summary>world節: マスタに無いブロックを除去し、残ったブロックのstate内のアイテム参照を空にする</summary>
    /// <summary>The world section: removes blocks absent from the master, then empties item references inside the remaining blocks' state</summary>
    public sealed class WorldSectionPruner : IMissingMasterSectionPruner
    {
        public string SaveSectionName => "world";

        public MissingMasterSectionPruneResult Prune(JToken section)
        {
            var result = new MissingMasterSectionPruneResult();
            // 配列でないworld節は除去できない。素通しを黙認すると不発に気づけない
            // A non-array world section cannot be pruned, and a silent pass would hide the no-op
            if (section is not JArray world)
            {
                Debug.Log($"world節が配列でないためブロックとstate内アイテムの除去を行いません。 type={section.Type}");
                return result;
            }

            // ブロック除去を先に行う。除去したブロックの中身まで空スタックとして二重に数えない
            // Remove blocks first so the contents of a removed block are not counted again as emptied stacks
            RemoveMissingBlocks();
            EmptyMissingItemsInBlockStates();
            return result;

            #region Internal

            void RemoveMissingBlocks()
            {
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
                    result.RemovedBlocks.Add(block.DeepClone());
                    block.Remove();
                }
            }

            void EmptyMissingItemsInBlockStates()
            {
                var cleaner = new MissingItemReferenceCleaner(result);
                var walker = new ItemStackPruneWalker(cleaner);
                foreach (var block in world.OfType<JObject>())
                {
                    // stateの無いブロックは中身を持たない。オブジェクトでないstateは形が壊れているので理由を残す
                    // A block without state holds nothing; a non-object state is malformed, so the reason is logged
                    var stateToken = block["state"];
                    if (stateToken == null || stateToken.Type == JTokenType.Null) continue;
                    if (stateToken is not JObject states)
                    {
                        Debug.LogWarning($"stateがオブジェクトでないためブロック内アイテムの除去を行いません。 instanceId={block["instanceId"]} type={stateToken.Type}");
                        continue;
                    }

                    // stateのキーはコンポーネントのSaveKey。住所の格納先はここから決まる
                    // Each state key is a component SaveKey, which anchors the container in the address
                    var instanceId = (int?)block["instanceId"];
                    foreach (var state in states.Properties().ToList())
                    {
                        walker.Walk(state.Value, PrunedItemOrigin.BlockState(SaveSectionName, instanceId, state.Name));
                    }
                }

                cleaner.LogRemovedItemReferences(SaveSectionName);
                walker.LogSkippedStrings(SaveSectionName);
            }

            #endregion
        }
    }
}

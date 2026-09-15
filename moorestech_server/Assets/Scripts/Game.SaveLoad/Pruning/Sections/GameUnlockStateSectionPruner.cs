using System;
using System.Linq;
using Core.Master;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning.Sections
{
    /// <summary>gameUnlockState節: マスタに無いアイテム・ブロックの解放状態を一覧から除去する</summary>
    /// <summary>The gameUnlockState section: removes unlock states of items and blocks absent from the master</summary>
    /// <summary>ItemUnlockStateHolder/BlockUnlockStateHolderのLoadは欠損guidを読み飛ばすのでロードは止まらない。除去は退避と次回セーブの整合のため</summary>
    /// <summary>ItemUnlockStateHolder/BlockUnlockStateHolder.Load already skip missing guids so the load never stops; pruning keeps the archive and the next save consistent</summary>
    public sealed class GameUnlockStateSectionPruner : IMissingMasterSectionPruner
    {
        // 解放状態の各要素のguidの綴り（ItemUnlockStateInfoJsonObject/BlockUnlockStateInfoJsonObjectの[JsonProperty("guid")]）
        // The guid spelling of each unlock entry ([JsonProperty("guid")] on ItemUnlockStateInfoJsonObject/BlockUnlockStateInfoJsonObject)
        private const string EntryGuidKey = "guid";
        private const string ItemUnlockStatesKey = "itemUnlockStateInfos";
        private const string BlockUnlockStatesKey = "blockUnlockStateInfos";

        // 対象外: craftRecipe/machineRecipe/challengeCategory はロードでマスタ解決しない。trainCar/connectTool はLoadが欠損を読み飛ばす
        // Out of scope: craftRecipe/machineRecipe/challengeCategory are not resolved against the master at load; trainCar/connectTool loaders skip missing entries

        public string SaveSectionName => "gameUnlockState";

        public MissingMasterSectionPruneResult Prune(JToken section)
        {
            var result = new MissingMasterSectionPruneResult();
            if (section is not JObject unlockState)
            {
                Debug.Log($"gameUnlockState節がオブジェクトでないため解放状態の除去を行いません。 type={section.Type}");
                return result;
            }

            PruneList(ItemUnlockStatesKey, UnlockTargetKind.Item);
            PruneList(BlockUnlockStatesKey, UnlockTargetKind.Block);
            return result;

            #region Internal

            void PruneList(string listKey, UnlockTargetKind kind)
            {
                if (unlockState[listKey] is not JArray entries)
                {
                    Debug.Log($"gameUnlockState.{listKey}が配列でないため解放状態の除去を行いません。 type={unlockState[listKey]?.Type}");
                    return;
                }

                foreach (var entry in entries.OfType<JObject>().ToList())
                {
                    // 読めないguidはロード側のGuid.Parseで落ちる破損値。除去判定できないので理由を残して残す
                    // An unreadable guid is corruption that Guid.Parse rejects at load; it cannot be judged, so it stays with a logged reason
                    var guidText = entry[EntryGuidKey]?.Value<string>();
                    if (!Guid.TryParse(guidText, out var guid))
                    {
                        Debug.LogWarning($"解放状態のguidが読めないため除去判定せず残します。 list={listKey} guid={guidText}");
                        continue;
                    }

                    if (ExistsInMaster(kind, guid)) continue;

                    Debug.LogWarning($"マスタに存在しない解放状態を除去します。 list={listKey} guid={guidText}");
                    result.RemovedUnlockStates.Add(new JObject { ["list"] = listKey, ["entry"] = entry.DeepClone() });
                    entry.Remove();
                }
            }

            bool ExistsInMaster(UnlockTargetKind kind, Guid guid)
            {
                return kind switch
                {
                    UnlockTargetKind.Item => MasterHolder.ItemMaster.ExistItemId(guid),
                    UnlockTargetKind.Block => MasterHolder.BlockMaster.GetBlockIdOrNull(guid) != null,
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
                };
            }

            #endregion
        }

        private enum UnlockTargetKind
        {
            Item,
            Block,
        }
    }
}

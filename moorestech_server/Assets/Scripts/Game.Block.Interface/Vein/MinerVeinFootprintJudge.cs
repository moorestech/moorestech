using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.MineSettingsModule;
using UnityEngine;

namespace Game.Block.Interface.Vein
{
    /// <summary>
    ///     採掘機が掘れる鉱脈かを決める唯一の実装。クライアントの設置判定とサーバーの採掘対象決定が同じ合成規則を呼ぶ
    ///     The sole implementation deciding whether a miner can mine a vein; the client placement check and the server target selection call the same composed rule
    ///     位置の規則は BlockPositionInfoExtension.OverlapsVeinXz が正本で、ここはそれと mineSettings 一致を合成するだけ（ADR 0039）
    ///     BlockPositionInfoExtension.OverlapsVeinXz owns the positional rule; this only composes it with the mineSettings match (ADR 0039)
    /// </summary>
    public static class MinerVeinFootprintJudge
    {
        /// <summary>
        ///     掘れるアイテムIDを先に解決して呼び出し側が持ち回る。設置プレビューは毎フレーム回るためmaster引きをここへ寄せない
        ///     Resolve the minable item ids once and let the caller hold them; the placement preview runs every frame, so master lookups must not sit in the loop
        /// </summary>
        public static HashSet<ItemId> ResolveMinableItemIds(MineSettings mineSettings)
        {
            var minableItemIds = new HashSet<ItemId>();
            foreach (var miningSetting in mineSettings.items) minableItemIds.Add(MasterHolder.ItemMaster.GetItemId(miningSetting.ItemGuid));

            return minableItemIds;
        }

        /// <summary>
        ///     未対応鉱脈を対象に入れると採掘時間が決まらず毎tick産出するため、mineSettingsに無い鉱脈は掘れない
        ///     An unlisted vein has no mining time and would yield every tick, so only veins in mineSettings are minable
        /// </summary>
        public static bool IsMinableVein(BlockPositionInfo footprint, HashSet<ItemId> minableItemIds, Vector3Int veinMinCell, Vector3Int veinMaxCell, ItemId veinItemId)
        {
            return minableItemIds.Contains(veinItemId) && footprint.OverlapsVeinXz(veinMinCell, veinMaxCell);
        }
    }
}

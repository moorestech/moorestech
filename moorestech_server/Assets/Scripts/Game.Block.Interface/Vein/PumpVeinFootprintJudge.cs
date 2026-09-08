using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.GenerateFluidsModule;
using UnityEngine;

namespace Game.Block.Interface.Vein
{
    /// <summary>
    ///     ポンプが汲み上げられる鉱脈かを決める唯一の実装。クライアントの設置判定とサーバーの汲み上げ対象決定が同じ合成規則を呼ぶ
    ///     The sole implementation deciding whether a pump can draw from a vein; the client placement check and the server target selection call the same composed rule
    ///     位置の規則は BlockPositionInfoExtension.OverlapsVeinXz が正本で、ここはそれと generateFluid 一致を合成するだけ（ADR 0051）
    ///     BlockPositionInfoExtension.OverlapsVeinXz owns the positional rule; this only composes it with the generateFluid match (ADR 0051)
    /// </summary>
    public static class PumpVeinFootprintJudge
    {
        /// <summary>
        ///     汲み上げられる流体IDを先に解決して呼び出し側が持ち回る。設置プレビューは毎フレーム回るためmaster引きをここへ寄せない
        ///     Resolve the pumpable fluid ids once and let the caller hold them; the placement preview runs every frame, so master lookups must not sit in the loop
        /// </summary>
        public static HashSet<FluidId> ResolvePumpableFluidIds(GenerateFluids generateFluids)
        {
            var pumpableFluidIds = new HashSet<FluidId>();
            foreach (var entry in generateFluids.items)
            {
                if (entry.GenerateTime <= 0) continue;
                pumpableFluidIds.Add(MasterHolder.FluidMaster.GetFluidId(entry.FluidGuid));
            }

            return pumpableFluidIds;
        }

        /// <summary>
        ///     generateFluid に無い流体は生成量が定義されないため、一致する鉱脈だけを汲み上げ対象にする
        ///     A fluid absent from generateFluid has no generation amount, so only matching veins are pump targets
        /// </summary>
        public static bool IsPumpableVein(BlockPositionInfo footprint, HashSet<FluidId> pumpableFluidIds, Vector3Int veinMinCell, Vector3Int veinMaxCell, FluidId veinFluidId)
        {
            return pumpableFluidIds.Contains(veinFluidId) && footprint.OverlapsVeinXz(veinMinCell, veinMaxCell);
        }
    }
}

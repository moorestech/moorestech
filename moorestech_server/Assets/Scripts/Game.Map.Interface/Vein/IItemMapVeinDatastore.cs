using System.Collections.Generic;
using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IItemMapVeinDatastore
    {
        // 手掘り用セル包含判定（Y込み）。採掘機には未使用
        // Cell-containment check for hand mining (Y-inclusive); unused by miners
        public List<IItemMapVein> GetVeinsContainingCell(Vector3Int cell);

        // 採掘機の判定は鉱脈側では持たず、呼び出し側がMinerVeinFootprintJudgeで絞る（ADR 0039）
        // Miner judgement is not owned by the vein layer; callers filter with MinerVeinFootprintJudge (ADR 0039)
        public IReadOnlyList<IItemMapVein> Veins { get; }
    }
}
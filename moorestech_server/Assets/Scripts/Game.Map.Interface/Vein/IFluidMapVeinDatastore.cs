using System.Collections.Generic;

namespace Game.Map.Interface.Vein
{
    public interface IFluidMapVeinDatastore
    {
        // ポンプの判定は鉱脈側では持たず、呼び出し側がPumpVeinFootprintJudgeで絞る（ADR 0051）
        // Pump judgement is not owned by the vein layer; callers filter with PumpVeinFootprintJudge (ADR 0051)
        public IReadOnlyList<IFluidMapVein> Veins { get; }
    }
}

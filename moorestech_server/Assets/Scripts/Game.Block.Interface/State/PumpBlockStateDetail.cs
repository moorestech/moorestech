using System;
using System.Collections.Generic;
using Core.Master;
using MessagePack;

namespace Game.Block.Interface.State
{
    /// <summary>
    ///     ポンプの汲み上げ中流体と公称生成量。汲み上げ対象が無ければ空リスト（クライアントは鉱脈警告を出す）
    ///     The fluids a pump is drawing and their nominal rates; empty when it has no target (the client shows the vein warning)
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class PumpBlockStateDetail
    {
        public const string BlockStateDetailKey = "Pump";

        [Key(0)] public List<PumpingFluidMessagePack> PumpingFluids;

        public PumpBlockStateDetail(List<PumpingFluidMessagePack> pumpingFluids)
        {
            PumpingFluids = pumpingFluids;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PumpBlockStateDetail()
        {
        }
    }

    [MessagePackObject]
    public class PumpingFluidMessagePack
    {
        [Key(0)] public int FluidId { get; set; }

        // 充足率100%のときの秒あたり量。分間換算はクライアントが行う
        // Per-second amount at 100% satisfaction; the client converts to per-minute
        [Key(1)] public double AmountPerSecond { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PumpingFluidMessagePack()
        {
        }

        public PumpingFluidMessagePack(FluidId fluidId, double amountPerSecond)
        {
            FluidId = fluidId.AsPrimitive();
            AmountPerSecond = amountPerSecond;
        }
    }
}

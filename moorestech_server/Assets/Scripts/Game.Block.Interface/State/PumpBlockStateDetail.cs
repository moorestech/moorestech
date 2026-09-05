using System;
using System.Collections.Generic;
using Core.Master;
using MessagePack;

namespace Game.Block.Interface.State
{
    /// <summary>
    ///     汲み上げ中流体と公称量。無ければ空(クライアント警告)
    ///     Pumping fluids and nominal rates; empty triggers the client's vein warning
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

        // 満充足時の秒量。分換算はクライアント
        // Per-second amount at full satisfaction; client converts to per-minute
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

using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using MessagePack;

namespace Game.Block.Blocks.Pump
{
    // 電気・歯車の両ポンプで同一の汲み上げ中流体detailを1箇所で組み立てる
    // Builds the identical pumping-fluid detail shared by the electric and gear pumps in one place
    internal static class PumpStateDetailFactory
    {
        public static BlockStateDetail CreatePumpDetail(IReadOnlyList<FluidGenerationEntry> entries)
        {
            var pumpingFluids = new List<PumpingFluidMessagePack>();
            foreach (var entry in entries) pumpingFluids.Add(new PumpingFluidMessagePack(entry.FluidId, entry.PerSecond));

            var detail = new PumpBlockStateDetail(pumpingFluids);
            return new BlockStateDetail(PumpBlockStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail));
        }
    }
}

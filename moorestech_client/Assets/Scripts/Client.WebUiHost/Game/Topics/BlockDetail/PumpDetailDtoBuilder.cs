using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// ポンプの詳細DTOを構築する
    /// Composes the pump's electric-satisfaction and pumping-fluids state into its capability DTO
    /// </summary>
    public static class PumpDetailDtoBuilder
    {
        private const string ElectricKind = "electric";
        private const string GearKind = "gear";

        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param, CommonMachineBlockStateDetail common)
        {
            var pump = block.GetStateDetail<PumpBlockStateDetail>(PumpBlockStateDetail.BlockStateDetailKey);
            if (pump == null) return;

            // 種別の正はマスタのBlockParam。CommonMachineの有無で代用すると歯車ポンプに動力行が生え、油井では黙って消える
            // The master's BlockParam settles the kind; standing in CommonMachine's presence grows a power row on the gear pump and silently drops it on the oil well
            switch (param)
            {
                case ElectricPumpBlockParam:
                    // 油井の電力値はCommonMachineだけが持つ。前例MinerDetailDtoBuilderと同じく揃わない間は出さない
                    // Only CommonMachine carries the oil well's power values; like MinerDetailDtoBuilder, emit nothing until it is there
                    if (common == null) return;

                    dto.Pump = new PumpDetailDto
                    {
                        Kind = ElectricKind,
                        Electric = new PumpElectricDto { CurrentState = BlockDetailDtoBuilder.ToCamelCase(common.CurrentStateType), CurrentPower = common.CurrentPower, RequestPower = common.RequestPower },
                        PumpingFluids = BuildPumpingFluids(pump),
                    };
                    return;
                case GearPumpBlockParam:
                    // 歯車ポンプの動力はGearSectionが出すのでElectricを持たない
                    // The gear pump's power row belongs to GearSection, so it carries no Electric
                    dto.Pump = new PumpDetailDto { Kind = GearKind, PumpingFluids = BuildPumpingFluids(pump) };
                    return;
                default:
                    // Pump状態を配信する新種のBlockParamは種別の追加漏れなので即死させる
                    // A new BlockParam publishing Pump state means the kind mapping is missing, so fail fast
                    throw new System.InvalidOperationException($"[PumpDetailDtoBuilder] 未対応のポンプBlockParam: {param?.GetType().Name}");
            }
        }

        private static List<PumpingFluidDto> BuildPumpingFluids(PumpBlockStateDetail pump)
        {
            // 秒→分換算+表示用FluidGuid付与
            // Sec-to-minute conversion, plus display FluidGuid
            var result = new List<PumpingFluidDto>();
            foreach (var pumping in pump.PumpingFluids)
            {
                var fluidGuid = MasterHolder.FluidMaster.GetFluidMaster(new FluidId(pumping.FluidId)).FluidGuid.ToString("D");
                result.Add(new PumpingFluidDto { FluidId = pumping.FluidId, FluidGuid = fluidGuid, AmountPerMinute = (float)(pumping.AmountPerSecond * 60) });
            }
            return result;
        }
    }
}

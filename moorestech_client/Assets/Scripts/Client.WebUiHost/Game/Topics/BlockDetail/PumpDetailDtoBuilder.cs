using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Interface.State;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// ポンプの詳細DTOを構築する
    /// Composes the pump's electric-satisfaction and pumping-fluids state into its capability DTO
    /// </summary>
    public static class PumpDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, CommonMachineBlockStateDetail common)
        {
            // ポンプ: Pump StateDetail。油井は CommonMachine（電力充足）も併せて持つ（ADR 0051）
            // Pumps: the Pump state detail; the electric pump also carries CommonMachine for power satisfaction (ADR 0051)
            var pump = block.GetStateDetail<PumpBlockStateDetail>(PumpBlockStateDetail.BlockStateDetailKey);
            if (pump == null) return;

            dto.Pump = new PumpDetailDto
            {
                Electric = common == null ? null : new PumpElectricDto { CurrentState = BlockDetailDtoBuilder.ToCamelCase(common.CurrentStateType), CurrentPower = common.CurrentPower, RequestPower = common.RequestPower },
                PumpingFluids = BuildPumpingFluids(pump),
            };
        }

        private static List<PumpingFluidDto> BuildPumpingFluids(PumpBlockStateDetail pump)
        {
            // 公称量は秒→分に換算し、表示名解決用に FluidGuid を添える（採掘機の ItemsPerMinute と同じ意味）
            // Convert the nominal per-second rate to per-minute and attach the FluidGuid for name resolution (same meaning as the miner's ItemsPerMinute)
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

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
            // Pump StateDetail使用。油井はCommonMachineも持つ
            // Uses Pump StateDetail; the electric pump also carries CommonMachine
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

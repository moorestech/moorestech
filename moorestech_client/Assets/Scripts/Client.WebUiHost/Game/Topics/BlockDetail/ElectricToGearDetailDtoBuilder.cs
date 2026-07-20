using System.Collections.Generic;
using Client.Game.InGame.Block;
using Game.Block.Blocks.ElectricToGear;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 回転生成機の詳細DTOを構築する
    /// Composes ElectricToGearGenerator master modes and StateDetail into its capability DTO
    /// </summary>
    public static class ElectricToGearDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            if (param is not ElectricToGearGeneratorBlockParam electricParam) return;
            var state = block.GetStateDetail<ElectricToGearGeneratorBlockStateDetail>(
                ElectricToGearGeneratorBlockStateDetail.BlockStateDetailKey);
            if (state == null) return;

            // マスタ順をmodeIndexにする
            // Preserve master order on the wire as the stable meaning of modeIndex
            var outputModes = new List<ElectricToGearOutputModeDto>(electricParam.OutputModes.Length);
            foreach (var mode in electricParam.OutputModes)
            {
                outputModes.Add(new ElectricToGearOutputModeDto
                {
                    Rpm = mode.Rpm,
                    Torque = mode.Torque,
                    RequiredPower = mode.RequiredPower,
                });
            }

            dto.ElectricToGear = new ElectricToGearDetailDto
            {
                SelectedIndex = state.SelectedIndex,
                FulfillmentRate = state.ElectricFulfillmentRate,
                ConsumedElectricPower = state.ConsumedElectricPower,
                OutputModes = outputModes,
            };
        }
    }
}

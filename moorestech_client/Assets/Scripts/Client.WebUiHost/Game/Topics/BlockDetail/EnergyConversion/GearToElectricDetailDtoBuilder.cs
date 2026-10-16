using Client.Game.InGame.Block;
using Game.Block.Blocks.GearToElectric;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 回転発電機の状態詳細とマスタ最大量をDTOへ詰める
    /// Fills the rotary generator's state detail and master maximum into its DTO
    /// </summary>
    public static class GearToElectricDetailDtoBuilder
    {
        internal static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            if (param is not GearToElectricGeneratorBlockParam generatorParam) return;
            var state = block.GetStateDetail<GearToElectricGeneratorBlockStateDetail>(
                GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey);
            if (state == null)
            {
                Debug.LogWarning("GearToElectric の状態詳細が未着のため DTO を保留します");
                return;
            }
            dto.GearToElectric = BuildDetail(state, generatorParam);
        }

        // 判定済みの状態を写し、最大値だけマスタから引く
        // Copy the settled state and take only the maximum from master
        public static GearToElectricDetailDto BuildDetail(GearToElectricGeneratorBlockStateDetail state, GearToElectricGeneratorBlockParam param)
        {
            return new GearToElectricDetailDto
            {
                GenerationState = BlockDetailDtoBuilder.ToCamelCase(state.GenerationState.ToString()),
                SuppliedPower = state.GeneratedElectricPower,
                MaxGeneratedPower = (float)param.MaxGeneratedPower,
            };
        }
    }
}

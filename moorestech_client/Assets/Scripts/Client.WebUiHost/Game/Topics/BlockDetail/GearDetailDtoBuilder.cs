using Client.Game.InGame.Block;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 歯車の現在値と役割をDTOへ充填する
    /// Fills the gear's current values and role into the DTO
    /// </summary>
    public static class GearDetailDtoBuilder
    {
        private const string ConsumerRole = "consumer";
        private const string GeneratorRole = "generator";

        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            var gear = block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
            if (gear == null) return;

            dto.Gear = BuildGearDetail(gear, param);
        }

        /// <summary>
        /// 役割と基準RPMを1つの分岐で決める。正本はスキーマの IGearConsumptionParam で、持たないブロックは発電機
        /// One branch settles role and base RPM: the schema's IGearConsumptionParam is the authority, and blocks without it are generators
        /// </summary>
        public static GearDetailDto BuildGearDetail(GearStateDetail gear, object param)
        {
            var gearDto = new GearDetailDto
            {
                CurrentRpm = gear.CurrentRpm,
                CurrentTorque = gear.CurrentTorque,
            };

            // 基準RPMは消費側にしか存在しない。発電機の枝ではフィールドごとwireから省く
            // A base RPM exists only on the consumer side; the generator branch omits the field from the wire entirely
            if (param is IGearConsumptionParam consumptionParam)
            {
                gearDto.Role = ConsumerRole;
                gearDto.BaseRpm = (float)consumptionParam.GearConsumption.BaseRpm;
            }
            else
            {
                gearDto.Role = GeneratorRole;
            }

            return gearDto;
        }
    }
}

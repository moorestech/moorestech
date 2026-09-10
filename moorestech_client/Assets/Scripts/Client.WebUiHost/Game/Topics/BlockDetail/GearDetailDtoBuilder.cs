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

            // 役割の正本はスキーマの IGearConsumptionParam（具体型の列挙はしない）。持たないブロックは発電機
            // The schema's IGearConsumptionParam is the authority on role (no concrete-type enumeration); blocks without it are generators
            var consumptionParam = param as IGearConsumptionParam;
            var baseRpm = consumptionParam != null ? (float)consumptionParam.GearConsumption.BaseRpm : 0f;
            dto.Gear = new GearDetailDto
            {
                CurrentRpm = gear.CurrentRpm,
                CurrentTorque = gear.CurrentTorque,
                BaseRpm = baseRpm,
                Role = consumptionParam != null ? ConsumerRole : GeneratorRole,
            };
        }
    }
}

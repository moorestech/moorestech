using Client.Game.InGame.Block;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 歯車の現在値と役割（消費/発生）を capability DTO へ充填する
    /// Fills the gear's current values and its role (consumer/generator) into the capability DTO
    /// </summary>
    public static class GearDetailDtoBuilder
    {
        public const string ConsumerRole = "consumer";
        public const string GeneratorRole = "generator";

        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            var gear = block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
            if (gear == null) return;

            // 役割の正本はスキーマの IGearConsumptionParam（具体型の列挙はしない）。持たないブロックは発電機
            // The schema's IGearConsumptionParam is the authority on role (no concrete-type enumeration); blocks without it are generators
            var isConsumer = param is IGearConsumptionParam;
            var baseRpm = isConsumer ? (float)((IGearConsumptionParam)param).GearConsumption.BaseRpm : 0f;
            dto.Gear = new GearDetailDto
            {
                IsClockwise = gear.IsClockwise,
                CurrentRpm = gear.CurrentRpm,
                CurrentTorque = gear.CurrentTorque,
                BaseRpm = baseRpm,
                Role = isConsumer ? ConsumerRole : GeneratorRole,
            };
        }
    }
}

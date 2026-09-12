using Client.Game.InGame.Block;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

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
        /// 役割はサーバー送信のGearStateDetail.Roleを写し、基準RPMだけ消費側のマスタから引く
        /// Copies the server-sent GearStateDetail.Role and looks up base RPM from master only for consumers
        /// </summary>
        public static GearDetailDto BuildGearDetail(GearStateDetail gear, object param)
        {
            var gearDto = new GearDetailDto
            {
                CurrentRpm = gear.CurrentRpm,
                CurrentTorque = gear.CurrentTorque,
            };

            // 発電機の枝は基準RPMをwireから省く
            // The generator branch omits base RPM from the wire
            if (gear.Role == GearRole.Generator)
            {
                gearDto.Role = GeneratorRole;
                return gearDto;
            }

            // サーバーが消費側と言うのにマスタが消費パラメータを持たないのは不整合。歯車行を出さずログする
            // A server-declared consumer without a consumption param in master is inconsistent; skip the gear rows and log it
            if (param is not IGearConsumptionParam consumptionParam)
            {
                Debug.LogError($"[GearDetailDtoBuilder] Server reports GearRole.Consumer but block param {param?.GetType().Name} has no IGearConsumptionParam; gear rows are omitted");
                return null;
            }

            gearDto.Role = ConsumerRole;
            gearDto.BaseRpm = (float)consumptionParam.GearConsumption.BaseRpm;
            return gearDto;
        }
    }
}

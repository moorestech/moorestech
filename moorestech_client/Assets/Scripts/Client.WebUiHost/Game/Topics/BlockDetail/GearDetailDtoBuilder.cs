using System;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockInventory.Common;
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
            // 役割と消費の param は UITK と同じ 1 か所で読む
            // Read the role and consumption param at the same single place as UITK
            var drive = GearDriveConversion.Read(gear, param);
            var gearDto = new GearDetailDto
            {
                CurrentRpm = gear.CurrentRpm,
                CurrentTorque = gear.CurrentTorque,
            };
            switch (drive.Kind)
            {
                case GearDriveKind.Generator:
                    // 発電機の枝は基準RPMをwireから省く
                    // The generator branch omits base RPM from the wire
                    gearDto.Role = GeneratorRole;
                    return gearDto;
                case GearDriveKind.Consumer:
                    gearDto.Role = ConsumerRole;
                    gearDto.BaseRpm = (float)drive.ConsumptionParam.GearConsumption.BaseRpm;
                    return gearDto;
                case GearDriveKind.InconsistentConsumer:
                    // サーバーが消費側と言うのにマスタが消費パラメータを持たないのは不整合。歯車行を出さずログする
                    // A server-declared consumer without a consumption param in master is inconsistent; skip the gear rows and log it
                    Debug.LogError($"[GearDetailDtoBuilder] {drive.Anomaly}; gear rows are omitted");
                    return null;
                default:
                    // 歯車の状態は呼び手（Apply）が在るときだけ渡す
                    // The caller (Apply) passes the gear state only when it exists
                    throw new ArgumentOutOfRangeException(nameof(gear), drive.Kind, "BuildGearDetail needs a gear state detail");
            }
        }
    }
}

using Client.Game.InGame.Block;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Common
{
    /// <summary>
    /// 歯車駆動か（Web ホスト GearDetailDtoBuilder.Apply が歯車の DTO を作る条件の写し。Web は data.gear の有無で不足赤を決める）
    /// Whether the block is gear-driven (port of when the Web host's GearDetailDtoBuilder.Apply builds the gear DTO; Web picks shortage red from data.gear)
    /// </summary>
    public static class GearDriveConversion
    {
        public static bool IsGearDriven(BlockGameObject block, object param)
        {
            // 歯車の状態詳細が無いブロックは電力駆動（Web の data.gear === undefined と同じ）
            // A block without a gear state detail is electric (same as Web data.gear === undefined)
            var gear = block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
            if (gear == null) return false;
            if (gear.Role == GearRole.Generator) return true;

            // 消費側なのに param が消費の値を持たないのは不整合。Web と同じく電力駆動扱いにして Error ログ
            // A consumer without a consumption param is inconsistent; like Web, treat it as electric and log an error
            if (param is IGearConsumptionParam) return true;
            Debug.LogError($"[GearDriveConversion] Server reports GearRole.Consumer but block param {param?.GetType().Name} has no IGearConsumptionParam; treated as electric like the Web host");
            return false;
        }
    }
}

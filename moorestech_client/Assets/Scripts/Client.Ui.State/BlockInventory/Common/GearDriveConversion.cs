using Client.Game.InGame.Block;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Common
{
    /// <summary>
    /// 歯車駆動か
    /// Whether gear-driven; port of Web GearDetailDtoBuilder.Apply, which picks shortage red from data.gear
    /// </summary>
    public static class GearDriveConversion
    {
        public static bool IsGearDriven(BlockGameObject block, object param)
        {
            // 歯車の詳細が無ければ電力駆動
            // No gear state detail means electric, as Web data.gear === undefined
            var gear = block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
            if (gear == null) return false;
            if (gear.Role == GearRole.Generator) return true;

            // 消費の値が無いのは不整合
            // A consumer without a consumption param is inconsistent; like Web, treat as electric and log an error
            if (param is IGearConsumptionParam) return true;
            Debug.LogError($"[GearDriveConversion] Server reports GearRole.Consumer but block param {param?.GetType().Name} has no IGearConsumptionParam; treated as electric like the Web host");
            return false;
        }
    }
}

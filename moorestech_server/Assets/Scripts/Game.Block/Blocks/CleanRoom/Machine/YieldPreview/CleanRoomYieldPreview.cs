using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.OutputRewrite;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     工程の抽選に使う率を抽選と同じ口（CleanRoomRoleRates）から求める。表示・改善の差分・実抽選が同じ役割分岐を通る
    ///     Computes a process's draw rates through the draw's own entry point (CleanRoomRoleRates); display, improvement deltas and actual draws share one role dispatch
    /// </summary>
    internal static class CleanRoomYieldPreview
    {
        // Previewと実抽選を同じ役割分岐で選ぶ
        // Select preview and actual draw through one role dispatch
        internal static CleanRoomYieldPreviewResult Evaluate(CleanRoomRecipeRole role, CleanRoomClassYield classYield, MachineModuleEffect moduleEffect,
            CleanRoomYieldRollContext? roll, out List<IItemStack> outputs)
        {
            outputs = null;
            switch (role)
            {
                case LayerExposureRecipeRole layer:
                    if (roll.HasValue) outputs = LayerExposureOutputRewriter.Rewrite(layer, roll.Value);
                    return new LayerExposurePreview(CleanRoomRoleRates.LayerFailRate(layer, classYield, moduleEffect));
                case DicingRecipeRole dicing:
                    var dicingPreview = CleanRoomRoleRates.Dicing(dicing, classYield, moduleEffect);
                    if (roll.HasValue) outputs = DicingOutputRewriter.Rewrite(dicing, dicingPreview, roll.Value);
                    return dicingPreview;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role.GetType().Name, "未対応の清浄室工程の役割 / Unsupported clean-room recipe role");
            }
        }
    }
}

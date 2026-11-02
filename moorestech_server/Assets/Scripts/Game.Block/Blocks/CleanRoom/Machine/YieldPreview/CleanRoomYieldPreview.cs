using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.OutputRewrite;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface.Component;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     選択工程の次の抽選に使う率を、抽選と同じ口（TryGetRecipeRole・CleanRoomRoleRates）から求める
    ///     Computes the rates the next draw of the selected process will use, through the draw's own entry points (TryGetRecipeRole, CleanRoomRoleRates)
    /// </summary>
    internal static class CleanRoomYieldPreview
    {
        // selectedRecipe は選択済みのレシピ。未選択は呼び出し側が NoRolePreview にする
        // selectedRecipe is the selected recipe; the caller turns "unselected" into NoRolePreview
        public static CleanRoomYieldPreviewResult Compute(MachineRecipeMasterElement selectedRecipe, CleanRoomEffect cleanRoomEffect, MachineModuleEffect moduleEffect)
        {
            // 役割表に無いレシピは抽選しない
            // Recipes outside the role tables never draw
            if (!MasterHolder.CleanRoomMaster.TryGetRecipeRole(selectedRecipe.MachineRecipeGuid, out var role)) return NoRolePreview.Instance;

            // 止まっている部屋の効果はクラス値を持たないので、数字を作らず停止として返す
            // A halted room effect has no class values, so report it as halted instead of building numbers
            if (!cleanRoomEffect.TryGetClassYield(out var classYield)) return NotOperablePreview.Instance;

            return Evaluate(role, classYield, moduleEffect, null, out _);
        }

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
                    if (roll.HasValue) outputs = DicingOutputRewriter.Rewrite(dicing, roll.Value);
                    return CreateDicingPreview(dicing);
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role.GetType().Name, "未対応の清浄室工程の役割 / Unsupported clean-room recipe role");
            }

            #region Internal

            // シフト後の割合を [Lv1..LvMax] と失敗ダイに分けて記録する
            // Record the shifted rates split into [Lv1..LvMax] and the failed die
            DicingPreview CreateDicingPreview(DicingRecipeRole dicing)
            {
                var lossRate = CleanRoomRoleRates.DieLossRate(dicing, classYield, moduleEffect);
                var rates = CleanRoomRoleRates.DicingRates(dicing, classYield, moduleEffect, out var tier);
                var levelCount = dicing.DieLevelCount;
                return new DicingPreview(lossRate, tier, rates.Take(levelCount).ToArray(), rates[levelCount]);
            }

            #endregion
        }
    }
}

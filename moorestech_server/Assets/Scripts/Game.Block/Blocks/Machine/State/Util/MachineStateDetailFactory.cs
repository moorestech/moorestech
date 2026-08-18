using System;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using UnityEngine;

namespace Game.Block.Blocks.Machine.State.Util
{
    // 通常機械と清浄室機械で同一のstate詳細を1箇所で組み立てる
    // Builds the identical state details shared by the normal and clean-room machines in one place
    internal static class MachineStateDetailFactory
    {
        public static BlockStateDetail[] Create(MachineProcessContext context, ProcessingMachineProcessState processingState, ProcessState currentState, ProcessState lastState)
        {
            var processingRate = Mathf.Clamp01(processingState.TotalTicks > 0 ? 1f - (float)processingState.RemainingTicks / processingState.TotalTicks : 0f);

            // 充足率表示のためstateには基礎値でなくラッチ済みの実効要求電力を載せる（ADR 0010）
            // Publish the latched effective request power (not the base) so the client rate reads as satisfaction (ADR 0010)
            var commonMachineBlock = CommonMachineBlockStateDetail.CreateState(context.CurrentPower, context.PublishedRequestPower, processingRate, currentState.ToStr(), lastState.ToStr());
            var selectedRecipeGuid = context.SelectedRecipe?.MachineRecipeGuid ?? Guid.Empty;
            var machineBlock = MachineBlockStateDetail.CreateState(processingRate, processingState.RecipeGuid, selectedRecipeGuid);
            return new[] { commonMachineBlock, machineBlock };
        }
    }
}

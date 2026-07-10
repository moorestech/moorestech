using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.Machine.State
{
    // 加工ステート間で共有する状態を保持する単純なデータクラス
    // Simple data class holding the state shared across processing states
    internal class MachineProcessContext
    {
        public readonly VanillaMachineInputInventory InputInventory;
        public readonly VanillaMachineOutputInventory OutputInventory;
        public readonly MachineModuleEffectComponent EffectComponent;
        public readonly float RequestPower;
        public Guid? RecipeGuid;

        // このtickで各電力セグメントから供給された電力の加算器（次のUpdateでCurrentPowerへ確定）
        // Accumulator of power supplied by each energy segment this tick (latched into CurrentPower on the next Update)
        public float SuppliedPower;
        public float CurrentPower;

        public MachineProcessContext(
            VanillaMachineInputInventory inputInventory,
            VanillaMachineOutputInventory outputInventory,
            MachineModuleEffectComponent effectComponent,
            float requestPower,
            Guid? recipeGuid)
        {
            InputInventory = inputInventory;
            OutputInventory = outputInventory;
            EffectComponent = effectComponent;
            RequestPower = requestPower;
            RecipeGuid = recipeGuid;
        }

        public MachineRecipeChangeResult TrySetRecipe(Guid? recipeGuid, IOpenableInventory playerMainInventory, ProcessingMachineProcessState processingState)
        {
            if (recipeGuid == RecipeGuid) return MachineRecipeChangeResult.Success;

            // 指定時は対象機械と解放状態を検証する
            // Validate the target machine and unlock state when selecting a recipe
            if (recipeGuid.HasValue)
            {
                var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid.Value);
                if (recipe == null) return MachineRecipeChangeResult.RecipeNotFound;
                if (!InputInventory.IsRecipeForThisMachine(recipe)) return MachineRecipeChangeResult.RecipeForDifferentBlock;
                if (!InputInventory.IsRecipeUnlocked(recipe)) return MachineRecipeChangeResult.RecipeLocked;
            }

            // 返却を確定できた場合だけ選択を変更する
            // Change the selection only after a processing refund can be committed
            if (processingState.HasProcessing && !processingState.TryCancel(playerMainInventory))
                return MachineRecipeChangeResult.RefundCapacityInsufficient;
            RecipeGuid = recipeGuid;
            return MachineRecipeChangeResult.Success;
        }
    }

}

namespace Game.Block.Blocks.Machine
{
    public enum ProcessState
    {
        Idle,
        Processing,
        Halted,
    }

    public static class ProcessStateExtension
    {
        /// <summary>
        ///     ProcessStateを固定文字列へ変換し、Enum.ToStringのアロケーションを避ける。
        ///     Converts ProcessState to fixed strings and avoids allocations from Enum.ToString.
        /// </summary>
        public static string ToStr(this ProcessState state)
        {
            return state switch
            {
                ProcessState.Idle => VanillaMachineBlockStateConst.IdleState,
                ProcessState.Processing => VanillaMachineBlockStateConst.ProcessingState,
                ProcessState.Halted => VanillaMachineBlockStateConst.HaltedState,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }
    }
}

using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Mooresmaster.Model.MachineRecipesModule;

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

        // プレイヤーが選択したレシピ。未選択はnullで、その間は加工しない
        // Recipe selected by the player; null means unselected and the machine never processes
        public MachineRecipeMasterElement SelectedRecipe;

        // このtickで各電力セグメントから供給された電力の加算器（次のUpdateでCurrentPowerへ確定）
        // Accumulator of power supplied by each energy segment this tick (latched into CurrentPower on the next Update)
        public float SuppliedPower;
        public float CurrentPower;

        public MachineProcessContext(
            VanillaMachineInputInventory inputInventory,
            VanillaMachineOutputInventory outputInventory,
            MachineModuleEffectComponent effectComponent,
            float requestPower)
        {
            InputInventory = inputInventory;
            OutputInventory = outputInventory;
            EffectComponent = effectComponent;
            RequestPower = requestPower;
        }
    }
}

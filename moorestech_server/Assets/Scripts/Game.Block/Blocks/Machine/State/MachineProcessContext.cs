using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;

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

        public ProcessState CurrentState = ProcessState.Idle;
        public uint RemainingTicks;
        public float CurrentPower;
        public bool UsedPower;

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

using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.Machine.Inventory.Input
{
    /// <summary>
    ///     機械UIの停止理由「レベルの違う品が届いて止まっている」を答える部品。状態は入力インベントリの搬入口が持ち、加工状態は processor から読む。ここは読むだけ
    ///     Answers the machine UI's halt reason "an item of another level arrived and stalled"; the input inventory's inlet holds the state, the processor gives the process state, and this only reads them
    /// </summary>
    public class MachineInputLevelStallComponent : IBlockComponent
    {
        private readonly VanillaMachineInputInventory _inputInventory;
        private readonly IMachineProcessStateReader _processState;

        internal MachineInputLevelStallComponent(VanillaMachineInputInventory inputInventory, IMachineProcessStateReader processState)
        {
            _inputInventory = inputInventory;
            _processState = processState;
        }

        public bool TryGetStall(out MachineInputLevelStall stall)
        {
            BlockException.CheckDestroy(this);

            // 待機中だけ答える。加工中は次の加工の見込みにすぎず、出力詰まり・停止中は別の理由で止まっている
            // Answer only while idle; processing is just a forecast for the next job, and output blockage or halt stops it for another reason
            stall = default;
            if (_processState.CurrentState != ProcessState.Idle) return false;
            return _inputInventory.TryGetLevelStall(out stall);
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}

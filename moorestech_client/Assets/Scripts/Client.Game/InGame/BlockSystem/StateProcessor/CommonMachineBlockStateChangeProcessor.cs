using System;
using Game.Block.Blocks.Machine;
using Game.Block.Interface.State;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    /// TODO マシーン系は自動でつけるみたいなシステムが欲しいな、、、
    /// </summary>
    public class CommonMachineBlockStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            var state = blockState.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            var currentState = state.CurrentStateType;
            var previousState = state.PreviousStateType;
            
            switch (currentState)
            {
                case VanillaMachineBlockStateConst.ProcessingState:
                    if (previousState == VanillaMachineBlockStateConst.IdleState)
                    {
                    }
                    
                    break;
                case VanillaMachineBlockStateConst.IdleState:
                    break;
            }
        }
    }
}
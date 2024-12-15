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
        public CommonMachineBlockStateDetail CurrentMachineState { get; private set; }
        
        
        private void Awake()
        {
        }
        
        private void Start()
        {

        }
        
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            CurrentMachineState = blockState.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            var currentState = CurrentMachineState.CurrentStateType;
            var previousState = CurrentMachineState.PreviousStateType;
            
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
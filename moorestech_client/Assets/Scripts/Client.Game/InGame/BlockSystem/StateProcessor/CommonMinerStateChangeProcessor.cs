using Game.Block.Interface.State;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class CommonMinerStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        public CommonMinerBlockStateDetail CurrentGearState { get; private set; }
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            
            CurrentGearState = blockState.GetStateDetail<CommonMinerBlockStateDetail>(CommonMinerBlockStateDetail.BlockStateDetailKey);

        }
    }
}
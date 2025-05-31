using Game.Fluid;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class FluidPipeStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        public FluidPipeStateDetail FluidPipeState { get; private set; }
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            FluidPipeState = blockState.GetStateDetail<FluidPipeStateDetail>(FluidPipeStateDetail.FluidPipeStateDetailKey);
        }
    }
}
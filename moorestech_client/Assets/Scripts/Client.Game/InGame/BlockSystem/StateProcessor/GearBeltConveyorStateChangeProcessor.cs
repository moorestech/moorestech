using Client.Game.InGame.Block;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class GearBeltConveyorStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        public void Initialize(BlockGameObject blockGameObject) { }
        
        public void OnChangeState(BlockStateMessagePack blockState) { }
    }
}

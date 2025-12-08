using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChainPoleの接続用Collider
    /// Collider for GearChainPole connection selection
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GearChainPoleConnectAreaCollider : MonoBehaviour, IGearChainPoleConnectAreaCollider, IBlockGameObjectInnerComponent
    {
        private BlockGameObject _blockGameObject;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
        }

        public Vector3Int GetBlockPosition()
        {
            return _blockGameObject.BlockPosInfo.OriginalPos;
        }
    }
}

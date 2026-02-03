using Client.Game.InGame.Block;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    [RequireComponent(typeof(Collider))]
    public class TrainRailConnectAreaCollider : MonoBehaviour, IRailComponentConnectAreaCollider
    {
        public bool IsFront => isFront;
        [SerializeField] public bool isFront;
        
        public BlockGameObject BlockGameObject { get; private set; }
        
        public void Initialize(BlockGameObject blockGameObject)
        {
            BlockGameObject = blockGameObject;
        }
        
        
        public ConnectionDestination CreateConnectionDestination()
        {
            var origin = BlockGameObject.BlockPosInfo.OriginalPos;
            return new ConnectionDestination(origin, 0, isFront);
        }
    }
}

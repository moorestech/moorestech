using Client.Game.InGame.Block;
using Game.Train.RailGraph;
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
            var componentId = new RailComponentID(origin, 0);
            return new ConnectionDestination(componentId, isFront);
        }
    }
}

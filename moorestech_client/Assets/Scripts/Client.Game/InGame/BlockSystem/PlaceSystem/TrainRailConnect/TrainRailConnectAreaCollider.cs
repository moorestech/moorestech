using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    [RequireComponent(typeof(Collider))]
    public class TrainRailConnectAreaCollider : MonoBehaviour, IBlockGameObjectInnerComponent
    {
        public BlockGameObject BlockGameObject { get; private set; }
        
        public bool IsFront => isFront;
        [SerializeField] public bool isFront;
        
        public void Initialize(BlockGameObject blockGameObject)
        {
            BlockGameObject = blockGameObject;
        }
    }
}
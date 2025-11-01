using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    [RequireComponent(typeof(Collider))]
    public class TrainRailConnectAreaCollider : MonoBehaviour, IRailComponentConnectAreaCollider
    {
        public RailComponentSpecifierMode RailComponentSpecifierMode => RailComponentSpecifierMode.Rail;
        public bool IsFront => isFront;
        [SerializeField] public bool isFront;
        
        public BlockGameObject BlockGameObject { get; private set; }
        
        public void Initialize(BlockGameObject blockGameObject)
        {
            BlockGameObject = blockGameObject;
        }
        
        
        public RailComponentSpecifier CreateRailComponentSpecifier()
        {
            return RailComponentSpecifier.CreateRailSpecifier(BlockGameObject.BlockPosInfo.OriginalPos);
        }
    }
}
using Client.Game.InGame.Block;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public class StationRailConnectAreaCollider : MonoBehaviour, IRailComponentConnectAreaCollider
    {
        public RailComponentSpecifierMode RailComponentSpecifierMode => RailComponentSpecifierMode.Station;
        
        // 0番のBackと1番のFrontは駅の内部で繋がっているためこうなる
        // 0 is Back and 1 is Front, as they are connected inside the station.
        public bool IsFront => railNodeIndex == 0;
        
        /// <summary>
        /// 駅には2つのレールノードがあるため、そのインデックスを指定する。詳しくはドキュメント参照
        /// The station has two rail nodes, so specify the index. See the documentation for details.
        /// </summary>
        [SerializeField] private int railNodeIndex;
        
        
        public BlockGameObject BlockGameObject { get; private set; }
        
        public void Initialize(BlockGameObject blockGameObject)
        {
            BlockGameObject = blockGameObject;
        }
        
        
        public RailComponentSpecifier CreateRailComponentSpecifier()
        {
            return RailComponentSpecifier.CreateStationSpecifier(BlockGameObject.BlockPosInfo.OriginalPos, railNodeIndex);
        }
    }
}
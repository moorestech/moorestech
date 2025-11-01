using Client.Game.InGame.Block;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public class StationRailConnectAreaCollider : MonoBehaviour, IRailComponentConnectAreaCollider
    {
        public RailComponentSpecifierMode RailComponentSpecifierMode => RailComponentSpecifierMode.Station;
        
        // 1番のBackと0番のFrontは駅の内部で繋がっているためこうなる
        // 1 is Back and 0 is Front, as they are connected inside the station.
        public bool IsFront => railNodeIndex == 1;
        
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
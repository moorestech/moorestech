using Client.Game.InGame.Block;
using Client.Game.InGame.Train;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public class StationRailConnectAreaCollider : MonoBehaviour, IRailComponentConnectAreaCollider
    {
        // 1番のBackと0番のFrontは駅の内部で繋がっているためこうなる
        // 1 is Back and 0 is Front, as they are connected inside the station.
        public bool IsFront => railNodeIndex == StationRailNodeIndex.Index1;
        
        /// <summary>
        /// 駅には2つのレールノードがあるため、そのインデックスを指定する。詳しくはドキュメント参照
        /// The station has two rail nodes, so specify the index. See the documentation for details.
        /// </summary>
        [SerializeField] private StationRailNodeIndex railNodeIndex;
        
        
        public BlockGameObject BlockGameObject { get; private set; }
        
        public void Initialize(BlockGameObject blockGameObject)
        {
            BlockGameObject = blockGameObject;
        }
        
        
        public ConnectionDestination CreateConnectionDestination()
        {
            var origin = BlockGameObject.BlockPosInfo.OriginalPos;
            return new ConnectionDestination(origin, (int)railNodeIndex, IsFront);
        }
    }
    
    public enum StationRailNodeIndex
    {
        Index0 = 0,
        Index1 = 1,
    }
}

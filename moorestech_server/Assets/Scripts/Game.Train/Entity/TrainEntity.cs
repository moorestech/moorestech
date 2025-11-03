using Game.Entity.Interface;
using Game.Train.Train;
using Game.Train.RailGraph;
using UnityEngine;

namespace Game.Train.Entity
{
    /// <summary>
    /// 列車（TrainUnit）をエンティティ同期システムで扱うためのアダプター
    /// TrainUnitをIEntityインターフェースに適合させ、クライアントへの同期を可能にする
    /// Adapter to handle TrainUnit in entity synchronization system
    /// Adapts TrainUnit to IEntity interface to enable client synchronization
    /// </summary>
    public class TrainEntity : IEntity
    {
        private readonly TrainUnit _trainUnit;
        private readonly EntityInstanceId _instanceId;

        public EntityInstanceId InstanceId => _instanceId;
        public string EntityType => VanillaEntityType.VanillaTrain;

        /// <summary>
        /// TrainUnitからVector3座標を計算して返す
        /// RailPositionの2ノード間をLinear補間して列車の位置を算出
        /// Calculate and return Vector3 coordinates from TrainUnit
        /// Calculate train position by linear interpolation between two RailNodes
        /// </summary>
        public Vector3 Position
        {
            get
            {
                #region Position Calculation from RailPosition
                
                var railPosition = _trainUnit.RailPosition;
                if (railPosition == null) return Vector3.zero;

                // 向かっているノードと通過したノードを取得
                // Get approaching node and just passed node
                var approachingNode = railPosition.GetNodeApproaching();
                var passedNode = railPosition.GetNodeJustPassed();

                // ノードがnullの場合は原点を返す（エラーハンドリング）
                // Return origin if nodes are null (error handling)
                if (approachingNode == null) return Vector3.zero;
                if (passedNode == null) 
                {
                    // 単一ノード上にいる場合は、そのノードの座標を返す
                    // If on a single node, return that node's coordinates
                    return GetNodePosition(approachingNode);
                }

                // 各ノードの座標を取得
                // Get coordinates of each node
                var approachingPos = GetNodePosition(approachingNode);
                var passedPos = GetNodePosition(passedNode);

                // 2ノード間の距離を取得
                // Get distance between two nodes
                var totalDistance = passedNode.GetDistanceToNode(approachingNode);
                if (totalDistance <= 0) return approachingPos;

                // 次のノードまでの残り距離から進行度を計算
                // Calculate progress from remaining distance to next node
                var distanceToNext = railPosition.GetDistanceToNextNode();
                var t = 1.0f - (distanceToNext / (float)totalDistance);
                t = Mathf.Clamp01(t);

                // Linear補間で位置を計算
                // Calculate position with linear interpolation
                return Vector3.Lerp(passedPos, approachingPos, t);
                
                #endregion
            }
        }

        /// <summary>
        /// TrainIdを文字列化してStateとして返す
        /// クライアント側でマスターデータ検索に使用される
        /// Return TrainId as string for State
        /// Used for master data lookup on client side
        /// </summary>
        public string State => _trainUnit.TrainId.ToString();

        public TrainEntity(EntityInstanceId instanceId, TrainUnit trainUnit)
        {
            _instanceId = instanceId;
            _trainUnit = trainUnit;
        }

        /// <summary>
        /// 列車の位置はRailPositionで管理されるため、このメソッドは空実装
        /// 列車位置は外部から直接設定できない
        /// Train position is managed by RailPosition, so this method is empty
        /// Train position cannot be set directly from outside
        /// </summary>
        public void SetPosition(Vector3 serverVector3)
        {
            // 列車の位置はRailPositionで管理されるため、外部から設定しない
            // Train position is managed by RailPosition, not set from outside
        }

        #region Internal

        /// <summary>
        /// RailNodeから座標を取得する
        /// FrontControlPointのOriginalPositionを使用
        /// Get coordinates from RailNode
        /// Use OriginalPosition of FrontControlPoint
        /// </summary>
        private Vector3 GetNodePosition(RailNode node)
        {
            if (node?.FrontControlPoint == null) return Vector3.zero;
            return node.FrontControlPoint.OriginalPosition;
        }

        #endregion
    }
}


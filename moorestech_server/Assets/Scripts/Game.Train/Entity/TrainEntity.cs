using System;
using System.Collections.Generic;
using Core.Master;
using Game.Entity.Interface;
using Game.Train.Train;
using Game.Train.RailGraph;
using Game.Train.Utility;
using MessagePack;
using UnityEngine;

namespace Game.Train.Entity
{
    /// <summary>
    /// 列車（TrainUnit）をエンティティ同期システムで扱うためのアダプター
    /// あくまで仮なので、将来的には別のシステムで同期した方がいいかもしれない。
    ///
    /// レールの位置情報だけ返し、その値をクライアントでLerpした後に、ベジエ曲線をもとに具体的な座標を計算した方が、常にレールに沿った動きをして良いと思う。
    /// なのでGetTrainPositonはあくまで仮実装。今後廃止していく
    /// </summary>
    public class TrainEntity : IEntity
    {
        private readonly TrainCar _trainCar;
        private readonly TrainUnit _trainUnit;
        public EntityInstanceId InstanceId { get; }
        
        public string EntityType => VanillaEntityType.VanillaTrain;

        /// <summary>
        /// TrainUnitからVector3座標を計算して返す
        /// ベジエ曲線を考慮して列車の正確な位置を算出
        /// Calculate train position considering Bezier curve
        /// </summary>
        public Vector3 Position => GetTrainPositon();
        
        public TrainEntity(EntityInstanceId instanceId, TrainUnit trainUnit, TrainCar trainCar)
        {
            InstanceId = instanceId;
            _trainCar = trainCar;
            _trainUnit = trainUnit;
        }

        /// <summary>
        /// 列車の位置はRailPositionで管理されるため、このメソッドは空実装
        /// Train position is managed by RailPosition, so this method is empty
        /// </summary>
        public void SetPosition(Vector3 serverVector3) { }
        
        public byte[] GetEntityData()
        {
            // TODO 仮実装でとりあえず0番の車両としてマスターを設定しているけど、TrainCarを全てマスターベースに置き換える
            var tempTrainCarMasterGuid = MasterHolder.TrainUnitMaster.Train.TrainCars[0].TrainCarGuid;
            var railPositionMessagePack = CreateRailPositionMessagePack();
            var (previousCarId, nextCarId) = GetAdjacentCarIds();
            var state = new TrainEntityStateMessagePack(_trainCar.CarId, tempTrainCarMasterGuid, railPositionMessagePack, previousCarId, nextCarId);
            return MessagePackSerializer.Serialize(state);

            #region Internal

            RailPositionMessagePack CreateRailPositionMessagePack()
            {
                var railPosition = _trainUnit.RailPosition;
                var railNodes = railPosition.GetRailNodes();
                var railNodeDataList = new List<RailNodeDataMessagePack>();

                foreach (var node in railNodes)
                {
                    // RailNodeからConnectionDestinationを取得
                    // Get ConnectionDestination from RailNode
                    if (!RailGraphDatastore.TryGetConnectionDestination(node, out var connectionDest))
                    {
                        continue;
                    }

                    // 制御点を取得
                    // Get control point
                    var controlPoint = connectionDest.IsFront ? node.FrontControlPoint : node.BackControlPoint;
                    if (controlPoint == null) continue;

                    railNodeDataList.Add(new RailNodeDataMessagePack(
                        connectionDest.railComponentID.Position,
                        connectionDest.railComponentID.ID,
                        connectionDest.IsFront,
                        controlPoint.OriginalPosition,
                        controlPoint.ControlPointPosition
                    ));
                }

                return new RailPositionMessagePack(
                    railNodeDataList,
                    railPosition.DistanceToNextNode,
                    railPosition.TrainLength
                );
            }

            (Guid previousCarId, Guid nextCarId) GetAdjacentCarIds()
            {
                var cars = _trainUnit.Cars;
                var currentIndex = -1;

                // 現在の車両のインデックスを検索
                // Find current car index
                for (int i = 0; i < cars.Count; i++)
                {
                    if (cars[i].CarId == _trainCar.CarId)
                    {
                        currentIndex = i;
                        break;
                    }
                }

                if (currentIndex == -1) return (Guid.Empty, Guid.Empty);

                // 前方の車両（インデックスが小さい方 = 進行方向）
                // Previous car (smaller index = direction of travel)
                var prevId = currentIndex > 0 ? cars[currentIndex - 1].CarId : Guid.Empty;

                // 後方の車両（インデックスが大きい方）
                // Next car (larger index)
                var nextId = currentIndex < cars.Count - 1 ? cars[currentIndex + 1].CarId : Guid.Empty;

                return (prevId, nextId);
            }

            #endregion
        }
        
        
        [Obsolete("AIに書かせた列車の正確な位置を取得するための仮実装です。上部コメントの通り、将来的にはRailPositionを直接同期する方式に変更する予定です。")]
        private Vector3 GetTrainPositon()
        {
            var railPosition = _trainUnit.RailPosition;
            var railNodes = railPosition.TestGet_railNodes();
            
            // Bezier curve position calculation for train
            
            // ノードが2つ未満の場合はフォールバック
            // Fallback if less than 2 nodes
            if (railNodes.Count < 2)
            {
                if (railNodes.Count == 1 && railNodes[0]?.FrontControlPoint != null)
                {
                    return railNodes[0].FrontControlPoint.OriginalPosition;
                }
                return Vector3.zero;
            }
            
            var nextNode = railNodes[0];
            var currentNode = railNodes[1];
            
            // 制御点が存在しない場合のフォールバック
            // Fallback if control points don't exist
            if (nextNode?.FrontControlPoint == null || currentNode?.FrontControlPoint == null)
            {
                return nextNode?.FrontControlPoint?.OriginalPosition ?? Vector3.zero;
            }
            
            // セグメント長を取得
            // Get segment length
            int segmentLength = RailGraphDatastore.GetDistanceBetweenNodes(currentNode, nextNode);
            if (segmentLength <= 0)
            {
                return nextNode.FrontControlPoint.OriginalPosition;
            }
            
            // currentNodeからnextNodeへの進行度を計算（0.0 ～ 1.0）
            // Calculate progress from currentNode to nextNode (0.0 to 1.0)
            float distanceFromStart = segmentLength - railPosition.DistanceToNextNode;
            float t = Mathf.Clamp01(distanceFromStart / segmentLength);
            
            // ベジエ曲線の制御点を設定
            // Set up Bezier curve control points
            var cp0 = currentNode.FrontControlPoint;
            var cp1 = nextNode.BackControlPoint;
            
            var p0 = cp0.OriginalPosition;
            var p1 = cp0.ControlPointPosition;
            var p2 = cp1.ControlPointPosition;
            var p3 = cp1.OriginalPosition;
            
            // 相対座標系に変換（p0を原点とする）
            // Transform to relative coordinate system (p0 as origin)
            Vector3 originalP0 = p0;
            p3 -= p0;
            p0 = Vector3.zero;
            p2 += p3;
            
            // ベジエ曲線上の座標を計算
            // Calculate position on Bezier curve
            Vector3 relativePosition = BezierUtility.GetBezierPoint(p0, p1, p2, p3, t);
            
            // ワールド座標に戻す
            // Transform back to world coordinates
            return relativePosition + originalP0;
        }
    }
}

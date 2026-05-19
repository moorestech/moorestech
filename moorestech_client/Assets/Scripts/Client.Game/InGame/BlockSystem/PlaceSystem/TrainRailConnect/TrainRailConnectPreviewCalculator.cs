using System;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.Inventory.Main;
using Game.Train.RailCalc;
using Game.Train.SaveLoad;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.TrainModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// レール橋脚間接続時のプレビュー曲線を計算する
    /// Calculates the rail connection when connecting rail piers to each other
    /// </summary>
    public class TrainRailConnectPreviewCalculator
    {
        /// <summary>
        /// 終点がノードの場合
        /// When the endpoint is a node
        /// </summary>
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, ConnectionDestination to, RailGraphClientCache cache, ILocalPlayerInventory playerInventory, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            // 始点ノードを取得
            // Get the start node
            if (!cache.TryGetNodeId(from, out var fromNodeId) || !cache.TryGetNode(fromNodeId, out var fromNode))
            {
                return TrainRailConnectPreviewData.Invalid;
            }

            // 終点ノードを取得
            // Get the end node
            if (!cache.TryGetNodeId(to, out var toNodeId) || !cache.TryGetNode(toNodeId, out var toNode))
            {
                return TrainRailConnectPreviewData.Invalid;
            }

            // レール長から設置可能なレールを判定
            // Determine placeable rail items from curve length
            var length = BezierUtility.GetBezierCurveLength(fromNode, toNode, 64);
            (RailItemMasterElement element, int requiredCount)[] placeableRailItems = RailConnectionEditProtocol.GetPlaceableRailItems(playerInventory, length);
            var railTypeGuid = placeableRailItems.Length > 0 ? placeableRailItems[0].element.ItemGuid : Guid.Empty;

            // 両端ブロックから最大レール長を解決し、短い方を上限として比較
            // Resolve max rail length from both endpoint blocks and compare against the shorter one
            var fromMax = ResolveMaxConnectableRailLength(from, blockGameObjectDataStore);
            var toMax = ResolveMaxConnectableRailLength(to, blockGameObjectDataStore);
            var isLengthValid = length <= Mathf.Min(fromMax, toMax);

            // 描画用の制御点を生成
            // Build render control points
            BezierUtility.BuildRenderControlPoints(fromNode.FrontControlPoint, toNode.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
            return new TrainRailConnectPreviewData(p0, p1, p2, p3, railTypeGuid, placeableRailItems.Any(), isLengthValid);
        }

        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, Vector3 placePosition, RailComponentDirection direction, RailGraphClientCache cache, ILocalPlayerInventory playerInventory, BlockGameObjectDataStore blockGameObjectDataStore, float placingBlockMaxConnectableRailLength)
        {
            // 始点ノードを取得
            // Get the start node
            if (!cache.TryGetNodeId(from, out var fromNodeId) || !cache.TryGetNode(fromNodeId, out var fromNode))
            {
                return TrainRailConnectPreviewData.Invalid;
            }

            // 制御点計算に必要な位置と方向を取得
            // Get positions and directions for control points
            var startPosition = fromNode.FrontControlPoint.OriginalPosition;
            var endPosition = placePosition;
            var startDirection = fromNode.FrontControlPoint.ControlPointPosition;
            var endDirection = direction.ToVector3();
            if (endDirection.sqrMagnitude < 1e-6)
            {
                endDirection = new Vector3(0, 1f, 0);
            }
            else
            {
                endDirection.Normalize();
            }
            // 描画用の制御点を生成
            // Build render control points
            BezierUtility.BuildRenderControlPoints(startPosition, endPosition, startDirection, endDirection, out var p0, out var p1, out var p2, out var p3);
            var length = BezierUtility.GetBezierCurveLength(p0, p1, p2, p3, 64);
            (RailItemMasterElement element, int requiredCount)[] placeableRailItems = RailConnectionEditProtocol.GetPlaceableRailItems(playerInventory, length);
            var railTypeGuid = placeableRailItems.Length > 0 ? placeableRailItems[0].element.ItemGuid : Guid.Empty;

            // 始点側のブロックと配置予定ブロックの上限を比較
            // Compare limit between source block and the block to be placed
            var fromMax = ResolveMaxConnectableRailLength(from, blockGameObjectDataStore);
            var isLengthValid = length <= Mathf.Min(fromMax, placingBlockMaxConnectableRailLength);

            return new TrainRailConnectPreviewData(p0, p1, p2, p3, railTypeGuid, placeableRailItems.Any(), isLengthValid);
        }

        // ConnectionDestination が指すブロックから MaxConnectableRailLength を解決する
        // Resolve MaxConnectableRailLength from the block referenced by ConnectionDestination
        public static float ResolveMaxConnectableRailLength(ConnectionDestination dest, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            if (!blockGameObjectDataStore.TryGetBlockGameObject((Vector3Int)dest.blockPosition, out var blockGameObject)) return float.MaxValue;
            return GetMaxConnectableRailLength(blockGameObject.BlockMasterElement);
        }

        // BlockMasterElement の BlockParam が IRailEndpointBlockParam を実装している場合に値を取り出す
        // Read MaxConnectableRailLength via IRailEndpointBlockParam interface
        public static float GetMaxConnectableRailLength(BlockMasterElement element)
        {
            return element.BlockParam is IRailEndpointBlockParam param ? (float)param.MaxConnectableRailLength : float.MaxValue;
        }
    }

    public struct TrainRailConnectPreviewData : IEquatable<TrainRailConnectPreviewData>
    {
        public static TrainRailConnectPreviewData Invalid => new(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Guid.Empty, false, false, false);
        public Vector3 StartPoint;
        public Vector3 StartControlPoint;
        public Vector3 EndControlPoint;
        public Vector3 EndPoint;
        public Guid RailTypeGuid;
        public bool IsValid;
        public bool HasEnoughRailItem;
        public bool IsLengthValid;

        public TrainRailConnectPreviewData(Vector3 startPoint, Vector3 startControlPoint, Vector3 endControlPoint, Vector3 endPoint, Guid railTypeGuid, bool hasEnoughRailItem, bool isLengthValid, bool isValid = true)
        {
            StartPoint = startPoint;
            StartControlPoint = startControlPoint;
            EndControlPoint = endControlPoint;
            EndPoint = endPoint;
            IsValid = isValid;
            RailTypeGuid = railTypeGuid;
            HasEnoughRailItem = hasEnoughRailItem;
            IsLengthValid = isLengthValid;
        }
        public bool Equals(TrainRailConnectPreviewData other)
        {
            return StartPoint.Equals(other.StartPoint) && StartControlPoint.Equals(other.StartControlPoint) && EndControlPoint.Equals(other.EndControlPoint) && EndPoint.Equals(other.EndPoint) && RailTypeGuid.Equals(other.RailTypeGuid) && IsLengthValid == other.IsLengthValid;
        }
        public override bool Equals(object obj)
        {
            return obj is TrainRailConnectPreviewData other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(StartPoint, StartControlPoint, EndControlPoint, EndPoint, RailTypeGuid, IsLengthValid);
        }
        public override string ToString()
        {
            return $"({StartPoint}, {StartControlPoint}, {EndControlPoint}, {EndPoint})";
        }
    }
}

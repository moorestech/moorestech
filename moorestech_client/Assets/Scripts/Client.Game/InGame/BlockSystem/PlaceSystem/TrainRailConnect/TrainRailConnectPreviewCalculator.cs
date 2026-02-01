using System;
using System.Linq;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.Inventory.Main;
using Game.Train.RailCalc;
using Game.Train.SaveLoad;
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
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, ConnectionDestination to, RailGraphClientCache cache, ILocalPlayerInventory playerInventory)
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
            
            // 制御点計算に必要な位置と方向を取得
            // Get positions and directions for control points
            var startPosition = fromNode.FrontControlPoint.OriginalPosition;
            var endPosition = toNode.BackControlPoint.OriginalPosition;
            var startDirection = fromNode.FrontControlPoint.ControlPointPosition;
            var endDirection = toNode.BackControlPoint.ControlPointPosition;
            
            var p0 = startPosition;
            var p3 = endPosition;
            var strength = BezierUtility.CalculateSegmentStrength(p0, p3);
            var p1 = p0 + startDirection * strength;
            var p2 = p3 + endDirection * strength;
            return new TrainRailConnectPreviewData(p0, p1, p2, p3, railTypeGuid, placeableRailItems.Any());
        }
        
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, Vector3 cursorPosition, RailGraphClientCache cache, ILocalPlayerInventory playerInventory)
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
            var endPosition = cursorPosition;
            var startDirection = fromNode.FrontControlPoint.ControlPointPosition;
            var endDirection = startPosition - endPosition;
            if (endDirection.sqrMagnitude < 1e-6)
            {
                endDirection = new Vector3(0, 1f, 0);
            }
            else
            {
                endDirection.Normalize();
            }
            var p0 = startPosition;
            var p3 = endPosition;
            var strength = BezierUtility.CalculateSegmentStrength(p0, p3);
            var p1 = p0 + startDirection * strength;
            var p2 = p3 + endDirection * strength;
            var length = BezierUtility.GetBezierCurveLength(p0, p1, p2, p3, 64);
            (RailItemMasterElement element, int requiredCount)[] placeableRailItems = RailConnectionEditProtocol.GetPlaceableRailItems(playerInventory, length);
            var railTypeGuid = placeableRailItems.Length > 0 ? placeableRailItems[0].element.ItemGuid : Guid.Empty;
            return new TrainRailConnectPreviewData(p0, p1, p2, p3, railTypeGuid, placeableRailItems.Any());
        }
    }
    
    public struct TrainRailConnectPreviewData : IEquatable<TrainRailConnectPreviewData>
    {
        public static TrainRailConnectPreviewData Invalid => new(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Guid.Empty, false, false); 
        public Vector3 StartPoint;
        public Vector3 StartControlPoint;
        public Vector3 EndControlPoint;
        public Vector3 EndPoint;
        public Guid RailTypeGuid;
        public bool IsValid;
        public bool HasEnoughRailItem;
        
        public TrainRailConnectPreviewData(Vector3 startPoint, Vector3 startControlPoint, Vector3 endControlPoint, Vector3 endPoint, Guid railTypeGuid, bool hasEnoughRailItem, bool isValid = true)
        {
            StartPoint = startPoint;
            StartControlPoint = startControlPoint;
            EndControlPoint = endControlPoint;
            EndPoint = endPoint;
            IsValid = isValid;
            RailTypeGuid = railTypeGuid;
            HasEnoughRailItem = hasEnoughRailItem;
        }
        public bool Equals(TrainRailConnectPreviewData other)
        {
            return StartPoint.Equals(other.StartPoint) && StartControlPoint.Equals(other.StartControlPoint) && EndControlPoint.Equals(other.EndControlPoint) && EndPoint.Equals(other.EndPoint) && RailTypeGuid.Equals(other.RailTypeGuid);
        }
        public override bool Equals(object obj)
        {
            return obj is TrainRailConnectPreviewData other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(StartPoint, StartControlPoint, EndControlPoint, EndPoint, RailTypeGuid);
        }
        public override string ToString()
        {
            return $"({StartPoint}, {StartControlPoint}, {EndControlPoint}, {EndPoint})";
        }
    }
}

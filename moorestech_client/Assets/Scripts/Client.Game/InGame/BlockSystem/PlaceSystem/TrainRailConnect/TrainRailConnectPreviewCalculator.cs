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
    /// レール橋脚同士が接続する際、レール同士がどのような接続をするかを計算します
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
            // 起点ノードを取得
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
            
            // 起点の制御点
            // Start control point
            var startPosition = fromNode.FrontControlPoint.OriginalPosition;
            var endPosition = toNode.BackControlPoint.OriginalPosition;
            var startDirection = fromNode.FrontControlPoint.ControlPointPosition;
            var endDirection = toNode.BackControlPoint.ControlPointPosition;
            var strength = RailSegmentCurveUtility.CalculateSegmentStrength(startPosition, endPosition);
            RailSegmentCurveUtility.BuildControlPoints(startPosition, startDirection, endPosition, endDirection, strength, out var p0, out var p1, out var p2, out var p3);
            var length = BezierUtility.GetBezierCurveLength(p0, p1, p2, p3, 64);
            
            (RailItemMasterElement element, int requiredCount)[] placeableRailItems = RailConnectionEditProtocol.GetPlaceableRailItems(playerInventory, length);
            
            return new TrainRailConnectPreviewData(p0, p1, p2, p3, length, placeableRailItems.Any());
        }
        
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, Vector3 cursorPosition, RailGraphClientCache cache, ILocalPlayerInventory playerInventory)
        {
            // 起点ノードを取得
            // Get the start node
            if (!cache.TryGetNodeId(from, out var fromNodeId) || !cache.TryGetNode(fromNodeId, out var fromNode))
            {
                return TrainRailConnectPreviewData.Invalid;
            }
            
            // 起点の制御点
            // Start control point
            var startPosition = fromNode.FrontControlPoint.OriginalPosition;
            var endPosition = cursorPosition;
            var startDirection = fromNode.FrontControlPoint.ControlPointPosition;
            var endDirection = startPosition - endPosition;
            var strength = RailSegmentCurveUtility.CalculateSegmentStrength(startPosition, endPosition);
            RailSegmentCurveUtility.BuildControlPoints(startPosition, startDirection, endPosition, endDirection, strength, out var p0, out var p1, out var p2, out var p3);
            var length = BezierUtility.GetBezierCurveLength(p0, p1, p2, p3, 64);
            
            (RailItemMasterElement element, int requiredCount)[] placeableRailItems = RailConnectionEditProtocol.GetPlaceableRailItems(playerInventory, length);
            
            return new TrainRailConnectPreviewData(p0, p1, p2, p3, length, placeableRailItems.Any());
        }
    }
    
    public struct TrainRailConnectPreviewData : IEquatable<TrainRailConnectPreviewData>
    {
        public static TrainRailConnectPreviewData Invalid => new(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 0f, false, false); 
        public Vector3 StartPoint;
        public Vector3 StartControlPoint;
        public Vector3 EndControlPoint;
        public Vector3 EndPoint;
        public float Length;
        public bool IsValid;
        public bool HasEnoughRailItem;
        
        public TrainRailConnectPreviewData(Vector3 startPoint, Vector3 startControlPoint, Vector3 endControlPoint, Vector3 endPoint, float length, bool hasEnoughRailItem, bool isValid = true)
        {
            StartPoint = startPoint;
            StartControlPoint = startControlPoint;
            EndControlPoint = endControlPoint;
            EndPoint = endPoint;
            IsValid = isValid;
            Length = length;
            HasEnoughRailItem = hasEnoughRailItem;
        }
        public bool Equals(TrainRailConnectPreviewData other)
        {
            return StartPoint.Equals(other.StartPoint) && StartControlPoint.Equals(other.StartControlPoint) && EndControlPoint.Equals(other.EndControlPoint) && EndPoint.Equals(other.EndPoint);
        }
        public override bool Equals(object obj)
        {
            return obj is TrainRailConnectPreviewData other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(StartPoint, StartControlPoint, EndControlPoint, EndPoint);
        }
        public override string ToString()
        {
            return $"({StartPoint}, {StartControlPoint}, {EndControlPoint}, {EndPoint})";
        }
    }
}

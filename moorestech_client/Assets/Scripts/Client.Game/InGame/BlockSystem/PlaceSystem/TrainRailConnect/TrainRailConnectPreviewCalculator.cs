using System;
using Client.Game.InGame.Train;
using Game.Train.RailGraph;
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
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, ConnectionDestination to, RailGraphClientCache cache)
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
            var fromControlPoint = fromNode.FrontControlPoint;
            var p0 = fromControlPoint.OriginalPosition;
            var p1 = fromControlPoint.OriginalPosition + fromControlPoint.ControlPointPosition;
            var toControlPoint = toNode.BackControlPoint;
            var p2 = toControlPoint.OriginalPosition + toControlPoint.ControlPointPosition;
            var p3 = toControlPoint.OriginalPosition;
            
            return new TrainRailConnectPreviewData(p0, p1, p2, p3);
        }
        
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, Vector3 cursorPosition, RailGraphClientCache cache)
        {
            // 起点ノードを取得
            // Get the start node
            if (!cache.TryGetNodeId(from, out var fromNodeId) || !cache.TryGetNode(fromNodeId, out var fromNode))
            {
                return TrainRailConnectPreviewData.Invalid;
            }
            
            // 起点の制御点
            // Start control point
            var fromControlPoint = fromNode.FrontControlPoint;
            var p0 = fromControlPoint.OriginalPosition;
            var p1 = fromControlPoint.OriginalPosition + fromControlPoint.ControlPointPosition;
            var controlPointLength = Mathf.Min(fromControlPoint.ControlPointPosition.magnitude, (p1 - cursorPosition).magnitude);
            var toControlPointDirection = (p1 - cursorPosition).normalized;
            var p2 = cursorPosition + toControlPointDirection * controlPointLength;
            var p3 = cursorPosition;
            
            return new TrainRailConnectPreviewData(p0, p1, p2, p3);
        }
    }
    
    public struct TrainRailConnectPreviewData : IEquatable<TrainRailConnectPreviewData>
    {
        public static TrainRailConnectPreviewData Invalid => new(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, false); 
        public Vector3 StartPoint;
        public Vector3 StartControlPoint;
        public Vector3 EndControlPoint;
        public Vector3 EndPoint;
        public bool IsValid;
        
        public TrainRailConnectPreviewData(Vector3 startPoint, Vector3 startControlPoint, Vector3 endControlPoint, Vector3 endPoint, bool isValid = true)
        {
            StartPoint = startPoint;
            StartControlPoint = startControlPoint;
            EndControlPoint = endControlPoint;
            EndPoint = endPoint;
            IsValid = isValid;
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
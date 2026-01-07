using Client.Game.InGame.Train;
using Game.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// レール橋脚同士が接続する際、レール同士がどのような接続をするかを計算します
    /// Calculates how rails connect when connecting rail piers
    /// </summary>
    public class TrainRailConnectPreviewCalculator
    {
        /// <summary>
        ///     橋脚同士の接続プレビューデータを計算
        ///     Calculate preview data for pier-to-pier connection
        /// </summary>
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, ConnectionDestination to, RailGraphClientCache cache)
        {
            // 起点ノードの制御点を取得
            // Get control points from source node
            if (!cache.TryGetNodeId(from, out var fromNodeId) || !cache.TryGetNode(fromNodeId, out var fromNode))
            {
                return TrainRailConnectPreviewData.Invalid;
            }
            
            // 終点ノードの制御点を取得
            // Get control points from destination node
            if (!cache.TryGetNodeId(to, out var toNodeId) || !cache.TryGetNode(toNodeId, out var toNode))
            {
                return TrainRailConnectPreviewData.Invalid;
            }
            
            // 起点：IsFrontならFrontControlPoint、そうでなければBackControlPoint
            // Source: FrontControlPoint if IsFront, otherwise BackControlPoint
            var fromControlPoint = from.IsFront ? fromNode.FrontControlPoint : fromNode.BackControlPoint;
            var p0 = fromControlPoint.OriginalPosition;
            var p1 = fromControlPoint.ControlPointPosition + p0;
            
            // 終点：IsFrontならBackControlPoint（反対側）、そうでなければFrontControlPoint
            // Destination: BackControlPoint if IsFront (opposite side), otherwise FrontControlPoint
            var toControlPoint = to.IsFront ? toNode.BackControlPoint : toNode.FrontControlPoint;
            var p3 = toControlPoint.OriginalPosition;
            var p2 = toControlPoint.ControlPointPosition + p3;
            
            return new TrainRailConnectPreviewData(p0, p1, p2, p3);
        }
        
        /// <summary>
        ///     橋脚からカーソル位置への接続プレビューデータを計算
        ///     Calculate preview data for pier-to-cursor connection
        /// </summary>
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, Vector3 cursorPosition, RailGraphClientCache cache)
        {
            // 起点ノードの制御点を取得
            // Get control points from source node
            if (!cache.TryGetNodeId(from, out var fromNodeId) || !cache.TryGetNode(fromNodeId, out var fromNode))
            {
                return TrainRailConnectPreviewData.Invalid;
            }
            
            // 起点の制御点
            // Source control points
            var fromControlPoint = from.IsFront ? fromNode.FrontControlPoint : fromNode.BackControlPoint;
            var p0 = fromControlPoint.OriginalPosition;
            var p1 = fromControlPoint.ControlPointPosition + p0;
            
            // 終点はカーソル位置、制御点は起点方向に一定距離戻した位置
            // Destination is cursor position, control point is offset back towards source
            var p3 = cursorPosition;
            var direction = (p3 - p0).normalized;
            var controlDistance = Vector3.Distance(p0, p1); // 既存の
            var p2 = p3 - direction * controlDistance;
            
            return new TrainRailConnectPreviewData(p0, p1, p2, p3);
        }
    }
    
    /// <summary>
    ///     レール接続プレビュー用のベジエ曲線制御点データ
    ///     Bezier curve control points for rail connection preview
    /// </summary>
    public struct TrainRailConnectPreviewData
    {
        public static readonly TrainRailConnectPreviewData Invalid = new(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, false);
        
        public readonly Vector3 P0; // 起点位置 / Start position
        public readonly Vector3 P1; // 起点制御点 / Start control point
        public readonly Vector3 P2; // 終点制御点 / End control point
        public readonly Vector3 P3; // 終点位置 / End position
        public readonly bool IsValid;
        
        public TrainRailConnectPreviewData(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, bool isValid = true)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
            IsValid = isValid;
        }
    }
}
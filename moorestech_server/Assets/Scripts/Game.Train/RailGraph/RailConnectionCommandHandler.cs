using System;
using Game.Train.Utility;
using UnityEngine; // 追加（ログ出力用）

namespace Game.Train.RailGraph
{
    /// <summary>
    ///     RailGraphDatastoreへの直接依存を避けつつ、nodeId+Guidベースの接続操作を担うハンドラー
    ///     Handler responsible for validating nodeId+Guid pairs and executing connect/disconnect commands
    /// </summary>
    public sealed class RailConnectionCommandHandler
    {
        private readonly bool _enableLog; // 追加（ログ抑制用。不要なら消してOK）

        public RailConnectionCommandHandler(RailGraphDatastore datastore, bool enableLog = true) // enableLog 引数を追加
        {
            // 依存解決順序のためインスタンス化を保証する
            // Ensure RailGraphDatastore is constructed via DI
            _ = datastore;

            _enableLog = enableLog; // 追加
        }

        // RailNode同士の接続を試行
        // Try to connect two rail nodes
        public bool TryConnect(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
        {
            if (!TryResolveNodes(fromNodeId, fromGuid, toNodeId, toGuid, out var fromNode, out var toNode))
            {
                return false;
            }

            var distance = CalculateSegmentDistance(fromNode, toNode);
            fromNode.ConnectNode(toNode, distance);
            ConnectOppositeNodes(fromNode, toNode, distance);
            return true;
        }

        // RailNode同士の接続解除を試行
        // Try to disconnect two rail nodes
        public bool TryDisconnect(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
        {
            if (!TryResolveNodes(fromNodeId, fromGuid, toNodeId, toGuid, out var fromNode, out var toNode))
            {
                return false;
            }

            fromNode.DisconnectNode(toNode);
            DisconnectOppositeNodes(fromNode, toNode);
            return true;
        }

        #region Internal

        private bool TryResolveNodes(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, out RailNode fromNode, out RailNode toNode)
        {
            fromNode = null;
            toNode = null;

            if (!RailGraphDatastore.TryGetRailNode(fromNodeId, out var resolvedFrom))
            {
                LogWarn($"fromNodeId not found. fromNodeId={fromNodeId}, fromGuid={fromGuid}, toNodeId={toNodeId}, toGuid={toGuid}");
                return false;
            }

            if (resolvedFrom.Guid != fromGuid)
            {
                LogWarn($"fromGuid mismatch. fromNodeId={fromNodeId}, expected(fromGuid)={fromGuid}, actual={resolvedFrom.Guid}, toNodeId={toNodeId}, toGuid={toGuid}");
                return false;
            }

            if (!RailGraphDatastore.TryGetRailNode(toNodeId, out var resolvedTo))
            {
                LogWarn($"toNodeId not found. fromNodeId={fromNodeId}, fromGuid={fromGuid}, toNodeId={toNodeId}, toGuid={toGuid}");
                return false;
            }

            if (resolvedTo.Guid != toGuid)
            {
                LogWarn($"toGuid mismatch. toNodeId={toNodeId}, expected(toGuid)={toGuid}, actual={resolvedTo.Guid}, fromNodeId={fromNodeId}, fromGuid={fromGuid}");
                return false;
            }

            fromNode = resolvedFrom;
            toNode = resolvedTo;
            return true;
        }

        private void LogWarn(string message) // 追加
        {
            if (!_enableLog) return;

            // エラーは出さず、logだけ
            Debug.LogWarning($"[RailConnectionCommandHandler] {message}");
            // Debug.Log($"[RailConnectionCommandHandler] {message}"); // Warningではなく通常ログにしたい場合はこちら
        }

        private static void ConnectOppositeNodes(RailNode fromNode, RailNode toNode, int distance)
        {
            var fromOpposite = fromNode.OppositeNode;
            var toOpposite = toNode.OppositeNode;
            if (fromOpposite == null || toOpposite == null)
            {
                return;
            }

            toOpposite.ConnectNode(fromOpposite, distance);
        }

        private static void DisconnectOppositeNodes(RailNode fromNode, RailNode toNode)
        {
            var fromOpposite = fromNode.OppositeNode;
            var toOpposite = toNode.OppositeNode;
            if (fromOpposite == null || toOpposite == null)
            {
                return;
            }

            toOpposite.DisconnectNode(fromOpposite);
        }

        private static int CalculateSegmentDistance(RailNode fromNode, RailNode toNode)
        {
            var length = BezierUtility.GetBezierCurveLength(fromNode.FrontControlPoint, toNode.BackControlPoint);
            return (int)(length * BezierUtility.RAIL_LENGTH_SCALE + 0.5f);
        }

        #endregion
    }
}

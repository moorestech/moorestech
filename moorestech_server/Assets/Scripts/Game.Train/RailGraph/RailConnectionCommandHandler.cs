using System;
using UnityEngine;
using Game.Train.RailCalc;

// 追加（ログ出力用）

namespace Game.Train.RailGraph
{
    /// <summary>
    ///     RailGraphDatastoreへの直接依存を避けつつ、nodeId+Guidベースの接続操作を担うハンドラー
    ///     Handler responsible for validating nodeId+Guid pairs and executing connect/disconnect commands
    /// </summary>
    public sealed class RailConnectionCommandHandler
    {
        private const float RailLengthScale = 1024.0f;
        private const int BezierSamples = 512;
        private readonly IRailGraphDatastore _datastore;
        private readonly bool _enableLog; // 追加（ログ抑制用。不要なら消してOK）

        public RailConnectionCommandHandler(IRailGraphDatastore datastore, bool enableLog = true) // enableLog 引数を追加
        {
            // 依存解決順序のためインスタンス化を保証する
            // Ensure RailGraphDatastore is constructed via DI
            _datastore = datastore;
            _enableLog = enableLog; // 追加
        }

        // RailNode同士の接続を試行
        // Try to connect two rail nodes
        public bool TryConnect(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, Guid railTypeGuid)
        {
            if (!TryResolveNodes(fromNodeId, fromGuid, toNodeId, toGuid, out var fromNode, out var toNode))
            {
                return false;
            }

            // 同一ノード/裏ノードへの接続を禁止
            // Reject connections targeting the same or opposite node
            if (fromNodeId == toNodeId || fromNodeId == (toNodeId ^ 1)) return false;
            fromNode.ConnectNode(toNode, railTypeGuid);
            ConnectOppositeNodes(fromNode, toNode, railTypeGuid);
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
        
        public bool TryResolveNodes(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, out RailNode fromNode, out RailNode toNode)
        {
            fromNode = null;
            toNode = null;

            if (!_datastore.TryGetRailNode(fromNodeId, out var resolvedFrom))
            {
                LogWarn($"fromNodeId not found. fromNodeId={fromNodeId}, fromGuid={fromGuid}, toNodeId={toNodeId}, toGuid={toGuid}");
                return false;
            }

            if (resolvedFrom.Guid != fromGuid)
            {
                LogWarn($"fromGuid mismatch. fromNodeId={fromNodeId}, expected(fromGuid)={fromGuid}, actual={resolvedFrom.Guid}, toNodeId={toNodeId}, toGuid={toGuid}");
                return false;
            }

            if (!_datastore.TryGetRailNode(toNodeId, out var resolvedTo))
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

        #region Internal

        private void LogWarn(string message) // 追加
        {
            if (!_enableLog) return;

            // エラーは出さず、logだけ
            Debug.LogWarning($"[RailConnectionCommandHandler] {message}");
            // Debug.Log($"[RailConnectionCommandHandler] {message}"); // Warningではなく通常ログにしたい場合はこちら
        }

        private static void ConnectOppositeNodes(RailNode fromNode, RailNode toNode, Guid railTypeGuid)
        {
            var fromOpposite = fromNode.OppositeRailNode;
            var toOpposite = toNode.OppositeRailNode;
            if (fromOpposite == null || toOpposite == null)
            {
                return;
            }

            toOpposite.ConnectNode(fromOpposite, railTypeGuid);
        }

        private static void DisconnectOppositeNodes(RailNode fromNode, RailNode toNode)
        {
            var fromOpposite = fromNode.OppositeRailNode;
            var toOpposite = toNode.OppositeRailNode;
            if (fromOpposite == null || toOpposite == null)
            {
                return;
            }

            toOpposite.DisconnectNode(fromOpposite);
        }

        #endregion
    }
}



using System;
using System.Collections.Generic;
using Client.Network.API;
using Server.Util.MessagePack;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     RailGraph差分の初期適用から再同期までを担うキャッシュ反映サービス
    ///     Service that applies the initial RailGraph snapshot and future resync payloads
    /// </summary>
    public sealed class RailGraphSnapshotApplier : IInitializable
    {
        private readonly RailGraphClientCache _cache;
        private readonly InitialHandshakeResponse _initialHandshakeResponse;

        public RailGraphSnapshotApplier(RailGraphClientCache cache, InitialHandshakeResponse initialHandshakeResponse)
        {
            _cache = cache;
            _initialHandshakeResponse = initialHandshakeResponse;
        }

        public void Initialize()
        {
            // 初回ハンドシェイクのスナップショットを即座に適用
            // Apply the handshake snapshot immediately after construction
            ApplySnapshot(_initialHandshakeResponse?.RailGraphSnapshot);
        }

        public void ApplySnapshot(RailGraphSnapshotMessagePack snapshot)
        {
            // スナップショットが空のときは何もしない
            // Skip when snapshot payload is missing or empty
            if (snapshot?.Nodes == null || snapshot.Nodes.Count == 0)
            {
                return;
            }

            // ノードと辺の最大IDを算出し、配列サイズを先に確定
            // Determine the max node id before allocating buffers
            var maxNodeId = ResolveMaxNodeId(snapshot);
            if (maxNodeId < 0)
            {
                return;
            }

            // 事前確保したコンテナにノード情報を流し込み
            // Populate prepared containers with node information
            var size = maxNodeId + 1;
            var nodeGuids = CreateList(size, Guid.Empty);
            var controlOrigins = CreateList(size, Vector3.zero);
            var primaryControls = CreateList(size, Vector3.zero);
            var oppositeControls = CreateList(size, Vector3.zero);
            var destinations = CreateList(size, ConnectionDestination.Default);
            var adjacency = CreateAdjacency(size);
            PopulateNodes(snapshot, size, nodeGuids, controlOrigins, primaryControls, oppositeControls, destinations);

            // 辺データを隣接リストへ登録
            // Apply edge information onto adjacency list
            PopulateConnections(snapshot, adjacency);

            // まとめてキャッシュへ反映
            // Commit prepared data to cache
            _cache.ApplySnapshot(
                nodeGuids,
                controlOrigins,
                adjacency,
                destinations,
                primaryControls,
                oppositeControls,
                snapshot.GraphTick);

            #region Internal

            List<List<(int targetId, int distance)>> CreateAdjacency(int capacity)
            {
                var result = new List<List<(int targetId, int distance)>>(capacity);
                for (var i = 0; i < capacity; i++)
                {
                    result.Add(new List<(int targetId, int distance)>());
                }

                return result;
            }

            void PopulateNodes(
                RailGraphSnapshotMessagePack targetSnapshot,
                int limit,
                List<Guid> guidList,
                List<Vector3> originList,
                List<Vector3> primaryList,
                List<Vector3> oppositeList,
                List<ConnectionDestination> destinationList)
            {
                // ノードごとの必須データを単純代入
                // Copy core node data with simple assignments
                foreach (var node in targetSnapshot.Nodes)
                {
                    if (node == null || node.NodeId < 0 || node.NodeId >= limit)
                    {
                        continue;
                    }

                    guidList[node.NodeId] = node.NodeGuid;
                    originList[node.NodeId] = node.OriginPoint?.ToUnityVector() ?? Vector3.zero;
                    primaryList[node.NodeId] = node.FrontControlPoint?.ToUnityVector() ?? Vector3.zero;
                    oppositeList[node.NodeId] = node.BackControlPoint?.ToUnityVector() ?? Vector3.zero;
                    destinationList[node.NodeId] = node.ConnectionDestination?.ToClientDestination() ?? ConnectionDestination.Default;
                }
            }

            void PopulateConnections(RailGraphSnapshotMessagePack targetSnapshot, List<List<(int targetId, int distance)>> adjacencyList)
            {
                // 辺が無ければ終了
                // Exit early when there are no edges
                if (targetSnapshot.Connections == null)
                {
                    return;
                }

                // 隣接リストへ追加
                // Append adjacency info to each origin node
                foreach (var connection in targetSnapshot.Connections)
                {
                    if (connection == null || connection.FromNodeId < 0 || connection.FromNodeId >= adjacencyList.Count)
                    {
                        continue;
                    }

                    adjacencyList[connection.FromNodeId].Add((connection.ToNodeId, connection.Distance));
                }
            }

            int ResolveMaxNodeId(RailGraphSnapshotMessagePack targetSnapshot)
            {
                // ノードと辺双方から最大IDを探索
                // Look at nodes and edges to find the max node id
                var max = -1;
                foreach (var node in targetSnapshot.Nodes)
                {
                    if (node == null)
                    {
                        continue;
                    }

                    max = Math.Max(max, node.NodeId);
                }

                if (targetSnapshot.Connections == null)
                {
                    return max;
                }

                foreach (var connection in targetSnapshot.Connections)
                {
                    if (connection == null)
                    {
                        continue;
                    }

                    max = Math.Max(max, Math.Max(connection.FromNodeId, connection.ToNodeId));
                }

                return max;
            }

            List<T> CreateList<T>(int capacity, T defaultValue)
            {
                // 既知サイズのリストを初期値で埋める
                // Pre-fill list with the provided default value
                var list = new List<T>(capacity);
                for (var i = 0; i < capacity; i++)
                {
                    list.Add(defaultValue);
                }

                return list;
            }

            #endregion
        }
    }
}

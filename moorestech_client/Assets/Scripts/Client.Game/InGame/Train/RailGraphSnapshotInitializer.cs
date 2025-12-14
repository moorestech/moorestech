using System;
using System.Collections.Generic;
using Client.Network.API;
using Server.Util.MessagePack;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     RailGraphスナップショットを適用してキャッシュを初期化する
    ///     Applies the rail graph snapshot to the client cache at startup
    /// </summary>
    public sealed class RailGraphSnapshotInitializer : IInitializable
    {
        private readonly RailGraphClientCache _cache;
        private readonly InitialHandshakeResponse _initialHandshakeResponse;

        public RailGraphSnapshotInitializer(RailGraphClientCache cache, InitialHandshakeResponse initialHandshakeResponse)
        {
            _cache = cache;
            _initialHandshakeResponse = initialHandshakeResponse;
        }

        public void Initialize()
        {
            ApplySnapshot();
        }

        private void ApplySnapshot()
        {
            var snapshot = _initialHandshakeResponse.RailGraphSnapshot;
            if (snapshot?.Nodes == null || snapshot.Nodes.Count == 0)
            {
                return;
            }

            var maxNodeId = -1;
            foreach (var node in snapshot.Nodes)
            {
                if (node == null)
                {
                    continue;
                }

                maxNodeId = Math.Max(maxNodeId, node.NodeId);
            }

            if (snapshot.Connections != null)
            {
                foreach (var connection in snapshot.Connections)
                {
                    if (connection == null)
                    {
                        continue;
                    }

                    maxNodeId = Math.Max(maxNodeId, Math.Max(connection.FromNodeId, connection.ToNodeId));
                }
            }

            if (maxNodeId < 0)
            {
                return;
            }

            var size = maxNodeId + 1;
            var nodeGuids = CreateList(size, Guid.Empty);
            var controlOrigins = CreateList(size, Vector3.zero);
            var primaryControls = CreateList(size, Vector3.zero);
            var oppositeControls = CreateList(size, Vector3.zero);
            var destinations = CreateList(size, ConnectionDestination.Default);
            var adjacency = new List<List<(int targetId, int distance)>>(size);
            for (var i = 0; i < size; i++)
            {
                adjacency.Add(new List<(int targetId, int distance)>());
            }

            foreach (var node in snapshot.Nodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (node.NodeId < 0 || node.NodeId >= size)
                {
                    continue;
                }

                nodeGuids[node.NodeId] = node.NodeGuid;
                controlOrigins[node.NodeId] = node.OriginPoint?.ToUnityVector() ?? Vector3.zero;
                primaryControls[node.NodeId] = node.FrontControlPoint?.ToUnityVector() ?? Vector3.zero;
                oppositeControls[node.NodeId] = node.BackControlPoint?.ToUnityVector() ?? Vector3.zero;
                destinations[node.NodeId] = node.ConnectionDestination?.ToClientDestination() ?? ConnectionDestination.Default;
            }

            if (snapshot.Connections != null)
            {
                foreach (var connection in snapshot.Connections)
                {
                    if (connection == null)
                    {
                        continue;
                    }

                    if (connection.FromNodeId < 0 || connection.FromNodeId >= adjacency.Count)
                    {
                        continue;
                    }

                    adjacency[connection.FromNodeId].Add((connection.ToNodeId, connection.Distance));
                }
            }

            _cache.ApplySnapshot(
                nodeGuids,
                controlOrigins,
                adjacency,
                destinations,
                primaryControls,
                oppositeControls,
                0);
        }

        private static List<T> CreateList<T>(int size, T defaultValue)
        {
            var list = new List<T>(size);
            for (var i = 0; i < size; i++)
            {
                list.Add(defaultValue);
            }

            return list;
        }
    }
}

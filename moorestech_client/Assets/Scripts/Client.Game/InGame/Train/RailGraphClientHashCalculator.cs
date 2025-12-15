using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    internal static class RailGraphClientHashCalculator
    {
        private const uint FnvOffset = 2166136261;
        private const uint FnvPrime = 16777619;

        public static uint Compute(
            IReadOnlyList<Guid> nodeGuids,
            IReadOnlyList<Vector3> controlOrigins,
            IReadOnlyList<Vector3> primaryControlPoints,
            IReadOnlyList<Vector3> oppositeControlPoints,
            IReadOnlyList<ConnectionDestination> destinations,
            IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> adjacency)
        {
            uint hash = FnvOffset;

            var nodeCount = nodeGuids?.Count ?? 0;
            for (var nodeId = 0; nodeId < nodeCount; nodeId++)
            {
                var guid = nodeGuids[nodeId];
                if (guid == Guid.Empty)
                {
                    continue;
                }

                hash = Mix(hash, unchecked((int)0x17F15C3D) ^ nodeId);
                hash = MixGuid(hash, guid);
                if (destinations != null && nodeId < destinations.Count)
                {
                    hash = MixDestination(hash, destinations[nodeId]);
                }

                if (controlOrigins != null && nodeId < controlOrigins.Count)
                {
                    hash = MixVector(hash, controlOrigins[nodeId]);
                }

                if (primaryControlPoints != null && nodeId < primaryControlPoints.Count)
                {
                    hash = MixVector(hash, primaryControlPoints[nodeId]);
                }

                if (oppositeControlPoints != null && nodeId < oppositeControlPoints.Count)
                {
                    hash = MixVector(hash, oppositeControlPoints[nodeId]);
                }
            }

            hash = Mix(hash, unchecked((int)0x3F6A2B1D));

            var nodeTotal = adjacency?.Count ?? 0;
            for (var nodeId = 0; nodeId < nodeTotal; nodeId++)
            {
                var edges = adjacency[nodeId];
                if (edges == null || edges.Count == 0)
                {
                    continue;
                }

                var normalized = new List<(int target, int dist)>(edges);
                normalized.Sort((a, b) =>
                {
                    var cmp = a.target.CompareTo(b.target);
                    return cmp != 0 ? cmp : a.dist.CompareTo(b.dist);
                });

                hash = Mix(hash, unchecked((int)0x7F00) ^ nodeId);
                foreach (var (target, distance) in normalized)
                {
                    hash = Mix(hash, target);
                    hash = Mix(hash, distance);
                }
            }

            return hash;
        }

        private static uint Mix(uint current, int value)
        {
            unchecked
            {
                current ^= (uint)value;
                current *= FnvPrime;
                return current;
            }
        }

        private static uint MixGuid(uint current, Guid guid)
        {
            var bytes = guid.ToByteArray();
            for (var i = 0; i < bytes.Length; i += 4)
            {
                var chunk = BitConverter.ToInt32(bytes, i);
                current = Mix(current, chunk);
            }

            return current;
        }

        private static uint MixVector(uint current, Vector3 vector)
        {
            current = Mix(current, FloatToInt(vector.x));
            current = Mix(current, FloatToInt(vector.y));
            current = Mix(current, FloatToInt(vector.z));
            return current;
        }

        private static uint MixDestination(uint current, ConnectionDestination destination)
        {
            if (destination.IsDefault)
            {
                return Mix(current, -1);
            }

            current = Mix(current, destination.ComponentPosition.x);
            current = Mix(current, destination.ComponentPosition.y);
            current = Mix(current, destination.ComponentPosition.z);
            current = Mix(current, destination.ComponentIndex);
            current = Mix(current, destination.IsFront ? 1 : 0);
            return current;
        }

        private static int FloatToInt(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }
    }
}

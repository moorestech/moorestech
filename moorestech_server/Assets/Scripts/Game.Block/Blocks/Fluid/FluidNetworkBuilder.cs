using System.Collections.Generic;
using Core.Master;
using Game.Fluid.Simulation;
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    // 再構築中にノード列と面の一覧を溜め、座標順に並べた面とポートから FluidSimNetwork の配列を作る
    // Accumulates node columns and the face list during a rebuild and produces the FluidSimNetwork arrays from faces and ports sorted by position
    public sealed class FluidNetworkBuilder
    {
        private readonly Vector3Int[] _positions;
        private readonly double[] _capacities;
        private readonly double[] _amounts;
        private readonly FluidId[] _fluidIds;
        private readonly List<FaceEntry> _faces = new();
        private readonly Dictionary<(Vector3Int a, Vector3Int b), int> _faceIndexByPositionPair = new();

        public FluidNetworkBuilder(int nodeCount)
        {
            _positions = new Vector3Int[nodeCount];
            _capacities = new double[nodeCount];
            _amounts = new double[nodeCount];
            _fluidIds = new FluidId[nodeCount];
        }

        public void SetNode(int index, Vector3Int position, double capacity, double amount, FluidId fluidId)
        {
            _positions[index] = position;
            _capacities[index] = capacity;
            _amounts[index] = amount;
            _fluidIds[index] = fluidId;
        }

        public Vector3Int PositionOf(int nodeIndex)
        {
            return _positions[nodeIndex];
        }

        // 既にあるペアの面に今回の走査向きを許可として足す。面が無ければ false
        // Adds this traversal direction as allowed on an existing face of the pair; false when the pair has no face yet
        public bool TryAllowDirection((Vector3Int a, Vector3Int b) key, bool pipeIsNodeA)
        {
            if (!_faceIndexByPositionPair.TryGetValue(key, out var index)) return false;
            var face = _faces[index];
            if (pipeIsNodeA) face.AllowAToB = true;
            else face.AllowBToA = true;
            _faces[index] = face;
            return true;
        }

        public void AddFace((Vector3Int a, Vector3Int b) key, int nodeA, int nodeB, double flowCapacityPerTick, double initialVelocity, bool pipeIsNodeA)
        {
            _faceIndexByPositionPair.Add(key, _faces.Count);
            _faces.Add(new FaceEntry { NodeA = nodeA, NodeB = nodeB, FlowCapacityPerTick = flowCapacityPerTick, Velocity = initialVelocity, AllowAToB = pipeIsNodeA, AllowBToA = !pipeIsNodeA });
        }

        // 面を正準側・相手側の座標順に並べて配列へ写し、網を作る
        // Sort faces by canonical-side then partner position, copy into arrays and build the network
        public FluidSimNetwork Build(IFluidBoundaryPort[] ports)
        {
            _faces.Sort((x, y) =>
            {
                var compare = Compare(_positions[x.NodeA], _positions[y.NodeA]);
                return compare != 0 ? compare : Compare(_positions[x.NodeB], _positions[y.NodeB]);
            });
            var faceA = new int[_faces.Count];
            var faceB = new int[_faces.Count];
            var faceCaps = new double[_faces.Count];
            var velocities = new double[_faces.Count];
            var allowAToB = new bool[_faces.Count];
            var allowBToA = new bool[_faces.Count];
            for (var i = 0; i < _faces.Count; i++)
            {
                var face = _faces[i];
                faceA[i] = face.NodeA;
                faceB[i] = face.NodeB;
                faceCaps[i] = face.FlowCapacityPerTick;
                velocities[i] = face.Velocity;
                allowAToB[i] = face.AllowAToB;
                allowBToA[i] = face.AllowBToA;
            }
            return new FluidSimNetwork(_positions, _capacities, _amounts, _fluidIds, faceA, faceB, faceCaps, velocities, allowAToB, allowBToA, ports);
        }

        private static int Compare(Vector3Int x, Vector3Int y)
        {
            if (x.x != y.x) return x.x.CompareTo(y.x);
            if (x.y != y.y) return x.y.CompareTo(y.y);
            return x.z.CompareTo(y.z);
        }

        private struct FaceEntry
        {
            public int NodeA;
            public int NodeB;
            public double FlowCapacityPerTick;
            public double Velocity;
            public bool AllowAToB;
            public bool AllowBToA;
        }
    }
}

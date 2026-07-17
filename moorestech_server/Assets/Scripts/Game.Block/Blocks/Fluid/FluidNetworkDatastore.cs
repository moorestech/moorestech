using System;
using System.Collections.Generic;
using Game.Fluid.Simulation;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     全パイプの登録と、シミュレーション用トポロジ（ノード・面・境界ポート）の構築を担うデータストア。
    ///     登録集合とdirtyフラグを持ち、tick先頭にServerTickUpdaterから呼ばれるRebuildIfDirtyでO(V+E)一括再構築する（electric・gearと同型）。
    ///     ブロックの増減は流体接続の増減なので、ワールド更新イベントでdirtyフラグを立て、次tick先頭で組み直す。
    ///     ノード・面は座標順にソートして保持し、走査順を決定論的にする（将来のクライアント同時計算の前提）。
    ///
    ///     Datastore registering every pipe and building the simulation topology (nodes, faces, boundary ports).
    ///     It keeps a registration set plus a dirty flag; RebuildIfDirty, called by ServerTickUpdater at the tick head, rebuilds everything in O(V+E) like electric and gear.
    ///     Any block place/remove can change fluid connections, so world update events mark the topology dirty for the next tick head.
    ///     Nodes and faces are kept position-sorted so iteration is deterministic (a prerequisite for future client-side co-simulation).
    /// </summary>
    public class FluidNetworkDatastore : IFluidNetworkDatastore
    {
        private readonly HashSet<FluidPipeComponent> _pipes = new();
        private readonly List<FluidPipeComponent> _sortedPipes = new();

        private readonly List<FluidSimNode> _nodes = new();
        private readonly List<FluidSimFace> _faces = new();
        private readonly List<FluidBoundaryPort> _boundaryPorts = new();
        private readonly Dictionary<(Vector3Int a, Vector3Int b), FluidSimFace> _faceByPositionPair = new();
        private bool _isTopologyDirty = true;

        public IReadOnlyList<FluidSimNode> Nodes => _nodes;
        public IReadOnlyList<FluidSimFace> Faces => _faces;
        public IReadOnlyList<FluidBoundaryPort> BoundaryPorts => _boundaryPorts;

        public FluidNetworkDatastore(IWorldBlockUpdateEvent worldBlockUpdateEvent)
        {
            worldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(_ => _isTopologyDirty = true);
            worldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(_ => _isTopologyDirty = true);
        }

        public void AddPipe(FluidPipeComponent pipe)
        {
            _pipes.Add(pipe);
            _isTopologyDirty = true;
        }

        public void RemovePipe(FluidPipeComponent pipe)
        {
            _pipes.Remove(pipe);
            _isTopologyDirty = true;
        }

        // セーブ用に、指定パイプが正準側(NodeA)として所有する面の方向と速度を集める
        // Collect directions and velocities of faces the given pipe owns as the canonical NodeA side, for saving
        public void CollectOwnedFaceVelocities(FluidPipeComponent pipe, List<(Vector3Int direction, double velocity)> buffer)
        {
            foreach (var face in _faces)
            {
                if (face.NodeA != pipe.Node) continue;
                buffer.Add((face.NodeB.Position - face.NodeA.Position, face.Velocity));
            }
        }

        // tick先頭にServerTickUpdaterから呼ばれ、登録・接続に変化があった時だけトポロジを一括再構築する
        // Called by ServerTickUpdater at the tick head; rebuilds the whole topology only when registrations or connections changed
        public void RebuildIfDirty()
        {
            if (!_isTopologyDirty) return;

            // パイプを座標順に整列し、ノード列を確定する
            // Sort pipes by position and settle the node list
            _sortedPipes.Clear();
            _sortedPipes.AddRange(_pipes);
            _sortedPipes.Sort(static (x, y) => ComparePositions(x.Position, y.Position));

            _nodes.Clear();
            foreach (var pipe in _sortedPipes) _nodes.Add(pipe.Node);

            // 既存面は位置ペアが一致しノードも同一なら速度ごと引き継ぐ（波の状態を再構築で失わない）
            // Reuse faces whose position pair and nodes match, carrying the velocity so rebuilds do not erase wave state
            var previousFaces = new Dictionary<(Vector3Int a, Vector3Int b), FluidSimFace>(_faceByPositionPair);
            _faceByPositionPair.Clear();
            _faces.Clear();
            _boundaryPorts.Clear();

            foreach (var pipe in _sortedPipes)
            {
                foreach (var (target, connectedInfo) in pipe.Connector.ConnectedTargets)
                {
                    if (target is FluidPipeComponent targetPipe)
                    {
                        if (!_pipes.Contains(targetPipe)) continue;
                        BuildFace(pipe, targetPipe, connectedInfo, previousFaces);
                    }
                    else
                    {
                        var flowCapacity = FluidBoundaryPort.GetFlowCapacityPerTick(connectedInfo);
                        var targetPosition = connectedInfo.TargetBlock.BlockPositionInfo.OriginalPos;
                        _boundaryPorts.Add(new FluidBoundaryPort(pipe.Node, target, connectedInfo, flowCapacity, targetPosition));
                    }
                }
            }

            _faces.Sort(static (x, y) => CompareFaces(x, y));
            _boundaryPorts.Sort(static (x, y) => ComparePorts(x, y));

            // ロード復元用の初期速度は最初の構築で消費済みになる
            // Loaded initial velocities are consumed by the first rebuild
            foreach (var pipe in _sortedPipes) pipe.ClearLoadedFaceVelocities();

            _isTopologyDirty = false;

            #region Internal

            void BuildFace(FluidPipeComponent pipe, FluidPipeComponent targetPipe, Game.Block.Interface.Component.ConnectedInfo connectedInfo, Dictionary<(Vector3Int a, Vector3Int b), FluidSimFace> previous)
            {
                // 正準側（座標辞書順で小さい側）をNodeAとする
                // NodeA is the canonical (lexicographically smaller position) side
                var (pipeA, pipeB) = ComparePositions(pipe.Position, targetPipe.Position) <= 0 ? (pipe, targetPipe) : (targetPipe, pipe);
                var key = (pipeA.Position, pipeB.Position);

                // 既にペアの面がある場合は今回の走査向き（pipe→targetPipe）を許可方向として追記する（双方向接続なら両向きが立つ）
                // If the pair's face exists, record this traversal direction (pipe→targetPipe) as allowed; bidirectional connections set both
                if (_faceByPositionPair.TryGetValue(key, out var existingFace))
                {
                    SetAllowedDirection(existingFace, pipe == pipeA);
                    return;
                }

                // 速度は前トポロジの同一面から引き継ぎ、無ければロード復元値を使う（波の状態を再構築で失わない）
                // Carry the velocity from the previous topology's matching face, else the loaded value, so rebuilds do not erase wave state
                var flowCapacity = FluidBoundaryPort.GetFlowCapacityPerTick(connectedInfo);
                var initialVelocity = previous.TryGetValue(key, out var previousFace) && previousFace.NodeA == pipeA.Node && previousFace.NodeB == pipeB.Node
                    ? previousFace.Velocity
                    : pipeA.TakeLoadedFaceVelocity(pipeB.Position - pipeA.Position);

                var face = new FluidSimFace(pipeA.Node, pipeB.Node, flowCapacity, initialVelocity);
                SetAllowedDirection(face, pipe == pipeA);
                _faceByPositionPair[key] = face;
                _faces.Add(face);

                static void SetAllowedDirection(FluidSimFace face, bool pipeIsNodeA)
                {
                    // 一方向パイプは片側の接続しか持たないため、存在する向きだけが許可される
                    // A one-way pipe holds a connection on one side only, so only the existing direction becomes allowed
                    if (pipeIsNodeA) face.AllowAToB = true;
                    else face.AllowBToA = true;
                }
            }

            static int CompareFaces(FluidSimFace x, FluidSimFace y)
            {
                var compare = ComparePositions(x.NodeA.Position, y.NodeA.Position);
                return compare != 0 ? compare : ComparePositions(x.NodeB.Position, y.NodeB.Position);
            }

            static int ComparePorts(FluidBoundaryPort x, FluidBoundaryPort y)
            {
                var compare = ComparePositions(x.PipeNode.Position, y.PipeNode.Position);
                return compare != 0 ? compare : ComparePositions(x.TargetPosition, y.TargetPosition);
            }

            static int ComparePositions(Vector3Int x, Vector3Int y)
            {
                if (x.x != y.x) return x.x.CompareTo(y.x);
                if (x.y != y.y) return x.y.CompareTo(y.y);
                return x.z.CompareTo(y.z);
            }

            #endregion
        }

        // 内容量が変化したパイプのBlockState通知をまとめて発火する
        // Fire batched BlockState notifications for pipes whose amount changed
        internal void NotifyChangedPipeStates()
        {
            foreach (var pipe in _sortedPipes)
            {
                // tick途中で破壊されたパイプへの通知はスキップする（gearと同じガード）
                // Skip pipes destroyed mid tick, mirroring the gear guard
                if (pipe.IsDestroy) continue;
                pipe.NotifyStateIfChanged();
            }
        }
    }
}

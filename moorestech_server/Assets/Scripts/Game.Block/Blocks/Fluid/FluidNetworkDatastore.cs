using System;
using System.Collections.Generic;
using Core.Master;
using Game.Fluid.Simulation;
using Game.Fluid.Sync.Diff;
using Game.Fluid.Sync.State;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     全パイプの登録と、シミュレーション用の網（ノード・面・境界ポートの配列）の構築を担うデータストア。
    ///     登録集合とdirtyフラグを持ち、tick末尾とロード完了時に呼ばれるRebuildIfDirtyでO(V+E)一括再構築する（electric・gearと同型）。
    ///     ブロックの増減は流体接続の増減なので、ワールド更新イベントでdirtyフラグを立て、そのtickの末尾で組み直す。
    ///     ノード・面は座標順に並べ、走査順を決定論的にする。クライアントの複製はこの並びをそのまま写して同じ計算を回す。
    ///
    ///     Datastore registering every pipe and building the simulation network (node, face and boundary-port arrays).
    ///     It keeps a registration set plus a dirty flag; RebuildIfDirty, called at the tick end and on load completion, rebuilds everything in O(V+E) like electric and gear.
    ///     Any block place/remove can change fluid connections, so world update events mark the topology dirty for that tick's end.
    ///     Nodes and faces are position-sorted so iteration is deterministic; the client replica copies this order and runs the same computation.
    /// </summary>
    public class FluidNetworkDatastore : IFluidNetworkDatastore, IFluidNetworkLookup
    {
        private readonly HashSet<FluidPipeComponent> _pipes = new();
        private readonly List<FluidPipeComponent> _sortedPipes = new();

        // 再構築のたびに網と作業配列を丸ごと置き換える。パイプは新しい番号を受け取る
        // Every rebuild swaps in a new network and scratch; pipes receive their new indices
        private FluidSimNetwork _network = FluidSimNetwork.CreateEmpty();
        private FluidSimulationScratch _scratch;
        private readonly FluidNetworkDiffRecorder _diffRecorder = new();
        private readonly Subject<Unit> _onTopologyRebuilt = new();
        private bool _isTopologyDirty = true;

        public FluidSimNetwork Network => _network;
        public FluidSimulationScratch Scratch => _scratch;
        public IObservable<Unit> OnTopologyRebuilt => _onTopologyRebuilt;

        // 直前の Step(または再構築)時点のハッシュ。クライアントの複製との照合に使う
        // Hash as of the last Step (or rebuild), checked against the client replica
        public uint CurrentStateHash => _network.LastStateHash;

        public FluidNetworkDatastore(IWorldBlockUpdateEvent worldBlockUpdateEvent)
        {
            _scratch = new FluidSimulationScratch(_network);
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

        // セーブ用に、指定パイプが正準側(NodeA)として所有する面の方向と速度を集める。未構築のパイプは面を持たない
        // Collect directions and velocities of faces the given pipe owns as the canonical NodeA side, for saving; an unbuilt pipe owns no face
        public void CollectOwnedFaceVelocities(FluidPipeComponent pipe, List<(Vector3Int direction, double velocity)> buffer)
        {
            if (pipe.NodeIndex < 0) return;
            for (var f = 0; f < _network.FaceCount; f++)
            {
                if (_network.FaceNodeA[f] != pipe.NodeIndex) continue;
                buffer.Add((_network.NodePositions[_network.FaceNodeB[f]] - _network.NodePositions[_network.FaceNodeA[f]], _network.FaceVelocities[f]));
            }
        }

        // 番号を持つパイプへの流入だけを差分に残す。設置直後で未構築のパイプは次の再構築の全量が中身ごと運ぶので記録しない
        // Only inflows into an indexed pipe go into the diff; a pipe placed and not yet built is carried, contents included, by the next rebuild's full state
        public void RecordInflow(FluidPipeComponent pipe, FluidId fluidId, double acceptedAmount)
        {
            if (pipe.NodeIndex < 0) return;
            _diffRecorder.RecordInflow(pipe.NodeIndex, fluidId, acceptedAmount);
        }

        // 流体tickの直後に、各ポートが機械へ実際に渡した量を差分へ写す
        // Right after the fluid tick, copy the amount each port actually handed to its machine into the diff
        public void RecordDeliveries()
        {
            _diffRecorder.RecordDeliveries(_network.Ports);
        }

        public FluidTickDiff TakeTickDiff()
        {
            return _diffRecorder.TakeTickDiff();
        }

        // 現在の網の全量を切り出す。初回同期・再構築時の全量送信が使う
        // Cuts the current network's full state; used for the initial sync and the full-state send after a rebuild
        public FluidNetworkFullState CaptureFullState()
        {
            return FluidNetworkFullStateCapture.Capture(_network);
        }

        // tick末尾とロード完了時に呼ばれ、登録・接続に変化があった時だけ網を一括再構築する
        // Called at the tick end and on load completion; rebuilds the whole network only when registrations or connections changed
        public void RebuildIfDirty()
        {
            if (!_isTopologyDirty) return;

            // パイプを座標順に整列し、旧網(または未構築の持ち越し)から中身を引き継いでノード列を作る
            // Sort pipes by position and build the node columns, carrying contents over from the old network (or the unbuilt holdover)
            _sortedPipes.Clear();
            _sortedPipes.AddRange(_pipes);
            _sortedPipes.Sort(static (x, y) => ComparePositions(x.Position, y.Position));
            var previous = _network;
            var builder = new FluidNetworkBuilder(_sortedPipes.Count);
            for (var i = 0; i < _sortedPipes.Count; i++)
            {
                var pipe = _sortedPipes[i];
                var (amount, fluidId) = pipe.TakeContents(previous);
                builder.SetNode(i, pipe.Position, pipe.Capacity, amount, fluidId);
                pipe.SetNodeIndex(i);
            }

            // 面は旧網の同じ位置ペアから速度を引き継ぐ(波の状態を再構築で失わない)。無ければロード復元値
            // Faces carry their velocity over from the old network's matching position pair (rebuilds do not erase wave state), else the loaded value
            var previousVelocities = new Dictionary<(Vector3Int a, Vector3Int b), double>(previous.FaceCount);
            for (var f = 0; f < previous.FaceCount; f++) previousVelocities[(previous.NodePositions[previous.FaceNodeA[f]], previous.NodePositions[previous.FaceNodeB[f]])] = previous.FaceVelocities[f];

            var ports = new List<FluidBoundaryPort>();
            foreach (var pipe in _sortedPipes)
            {
                foreach (var (target, connectedInfo) in pipe.Connector.ConnectedTargets)
                {
                    if (target is FluidPipeComponent targetPipe)
                    {
                        if (!_pipes.Contains(targetPipe)) continue;
                        AddFace(pipe, targetPipe, connectedInfo);
                    }
                    else
                    {
                        var flowCapacity = FluidBoundaryPort.GetFlowCapacityPerTick(connectedInfo);
                        var targetPosition = connectedInfo.TargetBlock.BlockPositionInfo.OriginalPos;
                        ports.Add(new FluidBoundaryPort(pipe.NodeIndex, target, connectedInfo, flowCapacity, targetPosition));
                    }
                }
            }

            ports.Sort((x, y) => ComparePorts(x, y));
            _network = builder.Build(ports.ToArray());
            _scratch = new FluidSimulationScratch(_network);

            // ロード復元用の初期速度は最初の構築で消費済みになる
            // Loaded initial velocities are consumed by the first rebuild
            foreach (var pipe in _sortedPipes) pipe.ClearLoadedFaceVelocities();

            _isTopologyDirty = false;
            _onTopologyRebuilt.OnNext(Unit.Default);

            #region Internal

            void AddFace(FluidPipeComponent pipe, FluidPipeComponent targetPipe, Game.Block.Interface.Component.ConnectedInfo connectedInfo)
            {
                // 正準側（座標辞書順で小さい側）をNodeAとする。既にペアの面があれば今回の走査向きを許可方向として追記する
                // NodeA is the canonical (lexicographically smaller position) side; if the pair's face exists, record this traversal direction as allowed
                var (pipeA, pipeB) = ComparePositions(pipe.Position, targetPipe.Position) <= 0 ? (pipe, targetPipe) : (targetPipe, pipe);
                var key = (pipeA.Position, pipeB.Position);
                var pipeIsNodeA = pipe == pipeA;
                if (builder.TryAllowDirection(key, pipeIsNodeA)) return;

                var flowCapacity = FluidBoundaryPort.GetFlowCapacityPerTick(connectedInfo);
                var initialVelocity = previousVelocities.TryGetValue(key, out var carried) ? carried : pipeA.TakeLoadedFaceVelocity(pipeB.Position - pipeA.Position);
                builder.AddFace(key, pipeA.NodeIndex, pipeB.NodeIndex, flowCapacity, initialVelocity, pipeIsNodeA);
            }

            int ComparePorts(FluidBoundaryPort x, FluidBoundaryPort y)
            {
                var compare = ComparePositions(builder.PositionOf(x.NodeIndex), builder.PositionOf(y.NodeIndex));
                return compare != 0 ? compare : ComparePositions(x.TargetPosition, y.TargetPosition);
            }

            #endregion
        }

        private static int ComparePositions(Vector3Int x, Vector3Int y)
        {
            if (x.x != y.x) return x.x.CompareTo(y.x);
            if (x.y != y.y) return x.y.CompareTo(y.y);
            return x.z.CompareTo(y.z);
        }
    }
}

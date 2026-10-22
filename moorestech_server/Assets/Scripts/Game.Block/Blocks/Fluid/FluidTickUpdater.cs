using System;
using Game.Fluid.Simulation;
using Game.Fluid.Sync.Diff;
using UniRx;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     流体tickの唯一の入口。MasterTickUpdaterからelectric・gearに続いて呼ばれる。
    ///     トポロジ反映は前tick末尾のRebuildIfDirty（MasterTickUpdater管轄）で済んでいる前提で、
    ///     全パイプを速度モデルで一括更新し、境界ポートが機械へ渡した量を差分に写す。
    ///     パイプ個別のUpdate（BlockSystem経由の自己tick）は存在せず、ここが全パイプの物理進行を担う。
    ///
    ///     Sole entry point of the fluid tick, called by MasterTickUpdater after electric and gear.
    ///     Topology changes are already applied by RebuildIfDirty at the previous tick end (owned by MasterTickUpdater);
    ///     this steps every pipe through the velocity model in one batch and copies what the boundary ports handed to machines into the diff.
    ///     Pipes have no per-block Update; all pipe physics advances here.
    /// </summary>
    public class FluidTickUpdater
    {
        private readonly FluidNetworkDatastore _fluidNetworkDatastore;
        private readonly Subject<FluidTickDiff> _onTickDiffPublished = new();

        // ブロック更新とベルト搬送の後に1回流れる。tickの束を送る側はこれを購読し、差分を連番つきで送る
        // Fires once after block updates and belt transport; the tick-bundle sender subscribes and sends the diff with a sequence id
        public IObservable<FluidTickDiff> OnTickDiffPublished => _onTickDiffPublished;

        public FluidTickUpdater(FluidNetworkDatastore fluidNetworkDatastore)
        {
            _fluidNetworkDatastore = fluidNetworkDatastore;
        }

        public void Update()
        {
            // 全ノード・全面をリープフロッグ2フェーズで一括更新する
            // Advance every node and face in one batched two-phase leapfrog step
            FluidSimulationStepper.Step(_fluidNetworkDatastore.Nodes, _fluidNetworkDatastore.Faces, _fluidNetworkDatastore.BoundaryPorts);

            // 各ポートが機械へ実際に渡した量をこのtickの差分へ残す
            // Keep the amount each port actually handed to its machine in this tick's diff
            _fluidNetworkDatastore.RecordDeliveries();
        }

        // 機械からパイプへの流入はブロック更新で起きるので、その後に差分を取り出して流す
        // Machine-to-pipe inflows happen during block updates, so the diff is taken and published after them
        public void PublishTickDiff()
        {
            _onTickDiffPublished.OnNext(_fluidNetworkDatastore.TakeTickDiff());
        }
    }
}

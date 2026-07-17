using Game.Fluid.Simulation;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     流体tickの唯一の入口。GameUpdater.AdditionalUpdatesに登録され、electric・gearに続いて走る。
    ///     tick先頭でトポロジ変更を反映し、全パイプを速度モデルで一括更新した後、変化したパイプの状態通知をまとめて発火する。
    ///     パイプ個別のUpdate（BlockSystem経由の自己tick）は存在せず、ここが全パイプの物理進行を担う。
    ///
    ///     Sole entry point of the fluid tick, registered in GameUpdater.AdditionalUpdates after electric and gear.
    ///     It flushes topology changes at the tick head, steps every pipe through the velocity model in one batch,
    ///     then fires batched state notifications for changed pipes. Pipes have no per-block Update; all pipe physics advances here.
    /// </summary>
    public class FluidTickUpdater
    {
        private readonly FluidNetworkDatastore _fluidNetworkDatastore;

        public FluidTickUpdater(FluidNetworkDatastore fluidNetworkDatastore)
        {
            _fluidNetworkDatastore = fluidNetworkDatastore;
        }

        public void Update()
        {
            // 保留中のパイプ登録/解除とブロック増減をトポロジへ反映する
            // Apply pending pipe registrations and block changes to the topology
            _fluidNetworkDatastore.FlushTopology();

            // 全ノード・全面をリープフロッグ2フェーズで一括更新する
            // Advance every node and face in one batched two-phase leapfrog step
            FluidSimulationStepper.Step(_fluidNetworkDatastore.Nodes, _fluidNetworkDatastore.Faces, _fluidNetworkDatastore.BoundaryPorts);

            // 内容量が変化したパイプの状態通知をまとめて発火する
            // Fire batched state notifications for pipes whose amount changed
            _fluidNetworkDatastore.NotifyChangedPipeStates();
        }
    }
}

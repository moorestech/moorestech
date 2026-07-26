using System;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Fluid;
using Game.Fluid.Simulation;
using Mooresmaster.Model.FluidInventoryConnectsModule;
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     パイプノードから非パイプ流体インベントリ（機械・発電機・列車プラットフォーム等）への流出ポート実装。
    ///     受け手には送り側connector視点のConnectedInfoを渡し、受け手が自分側（TargetConnector）のオプションでタンクを特定できるようにする。
    ///
    ///     Outflow port implementation from a pipe node to a non-pipe fluid inventory (machine, generator, train platform, ...).
    ///     The receiver gets the sender-side ConnectedInfo so it can resolve its own tank from its side (TargetConnector) options.
    /// </summary>
    public class FluidBoundaryPort : IFluidBoundaryPort
    {
        public FluidSimNode PipeNode { get; }
        public double FlowCapacityPerTick { get; }
        public double Velocity { get; set; }

        // トポロジ再構築時の決定論的な並び替えに使う
        // Used for deterministic ordering during topology rebuilds
        public readonly Vector3Int TargetPosition;

        private readonly IFluidInventory _target;
        private readonly ConnectedInfo _connectedInfo;

        public FluidBoundaryPort(FluidSimNode pipeNode, IFluidInventory target, ConnectedInfo connectedInfo, double flowCapacityPerTick, Vector3Int targetPosition)
        {
            PipeNode = pipeNode;
            _target = target;
            _connectedInfo = connectedInfo;
            FlowCapacityPerTick = flowCapacityPerTick;
            TargetPosition = targetPosition;
        }

        public FluidStack Deliver(FluidStack fluidStack)
        {
            return _target.AddLiquid(fluidStack, _connectedInfo);
        }

        // 両側コネクタの流体搬送能力の小さい方に、1tickの秒数を乗じた面あたり流量上限を求める
        // Per-face flow cap: the smaller of both connectors' flow capacities multiplied by seconds per tick
        public static double GetFlowCapacityPerTick(ConnectedInfo connectedInfo)
        {
            if (connectedInfo.SelfConnector is not IFluidConnector selfConnector || connectedInfo.TargetConnector is not IFluidConnector targetConnector)
            {
                throw new ArgumentException("Fluid connector option is not set");
            }

            return Math.Min(selfConnector.Option.FlowCapacity, targetConnector.Option.FlowCapacity) * GameUpdater.SecondsPerTick;
        }
    }
}

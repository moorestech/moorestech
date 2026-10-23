using System;
using Core.Master;
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
        public int NodeIndex { get; }
        public long FlowCapacityPerTick { get; }
        public long Velocity { get; set; }

        // 再構築時の決定論的な並び替えに使う
        // Used for deterministic ordering during rebuilds
        public readonly Vector3Int TargetPosition;

        private readonly IFluidInventory _target;
        private readonly ConnectedInfo _connectedInfo;

        public FluidBoundaryPort(int nodeIndex, IFluidInventory target, ConnectedInfo connectedInfo, long flowCapacityPerTick, Vector3Int targetPosition)
        {
            NodeIndex = nodeIndex;
            _target = target;
            _connectedInfo = connectedInfo;
            FlowCapacityPerTick = flowCapacityPerTick;
            TargetPosition = targetPosition;
        }

        // 単位の量を double にして機械へ渡し、機械が受け入れた double を切り上げて単位に戻す。ノードから引く量 ≥ 機械が得た量(最大 1 単位が消える側に丸め、増える側には丸めない)
        // Hands the units to the machine as a double and rounds the accepted double up back to units: the node loses at least what the machine gained (at most one unit vanishes; never rounds toward creating fluid)
        public long Deliver(long attemptedUnits, FluidId fluidId)
        {
            var attemptedAmount = FluidUnits.ToAmount(attemptedUnits);
            var remainder = _target.AddLiquid(new FluidStack(attemptedAmount, fluidId), _connectedInfo);
            var acceptedUnits = FluidUnits.ToUnitsCeil(attemptedAmount - remainder.Amount);
            return Math.Clamp(acceptedUnits, 0, attemptedUnits);
        }

        // 両側コネクタの流体搬送能力の小さい方に、1tickの秒数を乗じた面あたり流量上限(単位)を求める
        // Per-face flow cap (units): the smaller of both connectors' flow capacities multiplied by seconds per tick
        public static long GetFlowCapacityPerTick(ConnectedInfo connectedInfo)
        {
            if (connectedInfo.SelfConnector is not IFluidConnector selfConnector || connectedInfo.TargetConnector is not IFluidConnector targetConnector)
            {
                throw new ArgumentException("Fluid connector option is not set");
            }

            return FluidUnits.ToUnitsNearest(Math.Min(selfConnector.Option.FlowCapacity, targetConnector.Option.FlowCapacity) * GameUpdater.SecondsPerTick);
        }
    }
}

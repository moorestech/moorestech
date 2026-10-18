using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Fluid;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using JetBrains.Annotations;
using Mooresmaster.Model.FluidInventoryConnectsModule;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    internal static class TrainPlatformFluidTransfer
    {
        internal static double Transfer(FluidContainer from, FluidContainer to)
        {
            if (from.Amount < double.Epsilon) return 0; // silent-ok: 空タンクは通常状態 / 呼出元が移動量で通知を判断

            // 受入量だけ供給元から減らす
            // Deduct only the accepted amount from the source
            var fluidStack = new FluidStack(from.Amount, from.FluidId);
            var transferred = to.AddLiquid(fluidStack).AcceptedAmount;
            from.Amount -= transferred;

            if (from.Amount < double.Epsilon)
            {
                from.FluidId = FluidMaster.EmptyFluidId;
            }
            return transferred;
        }

        internal static bool IsTargetContainer(TrainCar dockedCar, [CanBeNull] FluidTrainCarContainer platformContainer, [CanBeNull] out FluidTrainCarContainer fluidContainer)
        {
            var trainContainer = dockedCar.Container;
            if (trainContainer is FluidTrainCarContainer f)
            {
                // 異液体の場合はアーム展開前に弾く
                // Reject before arm extension when fluids are incompatible
                if (platformContainer != null && HasIncompatibleFluids(platformContainer.Container, f.Container))
                {
                    fluidContainer = null;
                    return false; // silent-ok: 異液体は通常の非対象 / PFの状態表示で確認
                }
                fluidContainer = f;
                return true;
            }

            if (trainContainer is null)
            {
                fluidContainer = null;
                return true;
            }

            fluidContainer = null;
            return false; // silent-ok: 非液体貨車は通常の非対象 / 貨車表示で確認
        }

        private static bool HasIncompatibleFluids(FluidContainer platformFluid, FluidContainer trainFluid)
        {
            if (platformFluid.Amount < double.Epsilon || trainFluid.Amount < double.Epsilon) return false;
            return platformFluid.FluidId != trainFluid.FluidId;
        }

        internal static double PushToAdjacent(FluidContainer fluidContainer, BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>> connector)
        {
            if (fluidContainer.Amount < double.Epsilon) return 0; // silent-ok: 空タンクは通常状態 / 呼出元が移動量で通知を判断

            var totalTransferred = 0.0;

            // 各接続の流量上限で押し出す
            // Push within each connector flow limit
            foreach (var (inventory, info) in connector.ConnectedTargets)
            {
                if (fluidContainer.Amount < double.Epsilon) break;

                var flowRate = GetFlowRate(info);
                var transferAmount = Math.Min(fluidContainer.Amount, flowRate * GameUpdater.SecondsPerTick);
                if (transferAmount < double.Epsilon) continue; // silent-ok: ゼロ流量は通常状態 / タンク状態表示で確認

                var fluidStack = new FluidStack(transferAmount, fluidContainer.FluidId);
                var remain = inventory.AddLiquid(fluidStack, info);
                var transferred = transferAmount - remain.Amount;
                if (0 < transferred)
                {
                    fluidContainer.Amount -= transferred;
                    totalTransferred += transferred;
                }
            }

            if (fluidContainer.Amount < double.Epsilon)
            {
                fluidContainer.FluidId = FluidMaster.EmptyFluidId;
            }

            return totalTransferred;

            #region Internal

            double GetFlowRate(ConnectedInfo info)
            {
                if (info.SelfConnector is IFluidConnector fluidConnector)
                {
                    return fluidConnector.Option.FlowCapacity;
                }
                throw new ArgumentException("FluidConnectOption is not set on connector");
            }

            #endregion
        }

    }
}

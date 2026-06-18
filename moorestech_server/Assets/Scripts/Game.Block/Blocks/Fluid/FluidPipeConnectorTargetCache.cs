using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Blocks.Fluid
{
    internal class FluidPipeConnectorTargetCache
    {
        private readonly BlockConnectorComponent<IFluidInventory> _connectorComponent;
        private FluidPipeTransferTarget[] _targets = Array.Empty<FluidPipeTransferTarget>();

        public FluidPipeConnectorTargetCache(BlockConnectorComponent<IFluidInventory> connectorComponent)
        {
            _connectorComponent = connectorComponent;
        }

        public FluidPipeTransferTarget[] GetTargets()
        {
            var connectedTargets = _connectorComponent.ConnectedTargets;
            if (_targets.Length != connectedTargets.Count || HasDifferentTargets(connectedTargets))
            {
                Rebuild(connectedTargets);
            }

            return _targets;
        }

        public bool ContainsSourceContainer(FluidContainer sourceContainer)
        {
            var targets = GetTargets();
            foreach (var target in targets)
            {
                if (target.SourceContainer == sourceContainer) return true;
            }

            return false;
        }

        private bool HasDifferentTargets(IReadOnlyDictionary<IFluidInventory, ConnectedInfo> connectedTargets)
        {
            // 同数でも接続先が差し替わった場合は再構築する
            // Rebuild when targets changed even if the connected target count stayed the same.
            foreach (var target in _targets)
            {
                if (!connectedTargets.ContainsKey(target.Inventory)) return true;
            }

            return false;
        }

        private void Rebuild(IReadOnlyDictionary<IFluidInventory, ConnectedInfo> connectedTargets)
        {
            var index = 0;
            _targets = new FluidPipeTransferTarget[connectedTargets.Count];
            foreach (var connectedTarget in connectedTargets)
            {
                _targets[index] = new FluidPipeTransferTarget(
                    connectedTarget.Key,
                    GetSourceContainer(connectedTarget.Key),
                    CalculateMaxFlowAmountPerTick(connectedTarget.Value)
                );
                index++;
            }
        }

        private static FluidContainer GetSourceContainer(IFluidInventory inventory)
        {
            if (inventory is FluidPipeComponent pipe) return pipe.GetSourceIdentityContainer();
            return null;
        }

        private static double CalculateMaxFlowAmountPerTick(ConnectedInfo connectedInfo)
        {
            var selfOption = connectedInfo.SelfConnector?.ConnectOption as FluidConnectOption;
            var targetOption = connectedInfo.TargetConnector?.ConnectOption as FluidConnectOption;
            if (selfOption == null || targetOption == null) throw new ArgumentException();

            return Math.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * GameUpdater.SecondsPerTick;
        }
    }
}

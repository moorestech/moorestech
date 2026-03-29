using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using JetBrains.Annotations;
using MessagePack;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    public class TrainPlatformFluidContainerComponent : IUpdatableBlockComponent, IFluidInventory, IBlockSaveState
    {
        public bool IsDestroy { get; private set; }
        [CanBeNull] public FluidTrainCarContainer Container { get; private set; }

        private readonly TrainPlatformDockingComponent _dockingComponent;
        private readonly TrainPlatformTransferComponent _transferComponent;
        private readonly BlockConnectorComponent<IFluidInventory> _fluidConnector;
        private readonly double _capacity;

        public TrainPlatformFluidContainerComponent(
            TrainPlatformDockingComponent dockingComponent,
            TrainPlatformTransferComponent transferComponent,
            double capacity,
            BlockConnectorComponent<IFluidInventory> fluidConnector)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _fluidConnector = fluidConnector;
            _capacity = capacity;
        }

        public TrainPlatformFluidContainerComponent(
            TrainPlatformDockingComponent dockingComponent,
            TrainPlatformTransferComponent transferComponent,
            double capacity,
            BlockConnectorComponent<IFluidInventory> fluidConnector,
            Dictionary<string, string> componentStates)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _fluidConnector = fluidConnector;
            _capacity = capacity;

            if (componentStates.TryGetValue(SaveKey, out var serialized))
            {
                var serializedBytes = MessagePackSerializer.ConvertFromJson(serialized);
                var saveData = MessagePackSerializer.Deserialize<TrainPlatformFluidContainerSaveData>(serializedBytes);
                Container = saveData.Container;
            }
        }

        public void Update()
        {
            PushFluidToAdjacentBlocks();

            var dockedCar = _dockingComponent.DockedTrainCar;
            if (dockedCar == null) return;

            if (!IsTargetContainer(out var targetContainer)) return;

            switch (_transferComponent.Mode)
            {
                case TrainPlatformTransferComponent.TransferMode.LoadToTrain:
                    LoadFluidToTrain(dockedCar, targetContainer);
                    break;
                case TrainPlatformTransferComponent.TransferMode.UnloadToPlatform:
                    UnloadFluidFromTrain(dockedCar, targetContainer);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public List<FluidStack> GetFluidInventory()
        {
            if (Container == null) return new List<FluidStack>();
            
            var fluidContainer = Container.Container;
            var result = new List<FluidStack>();
            if (fluidContainer.Amount > 0)
            {
                result.Add(new FluidStack(fluidContainer.Amount, fluidContainer.FluidId));
            }
            return result;
        }

        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            if (Container == null) Container = new FluidTrainCarContainer(new FluidContainer(_capacity));

            return Container.Container.AddLiquid(fluidStack, source);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

        public string SaveKey { get; } = typeof(TrainPlatformFluidContainerComponent).FullName;

        public string GetSaveState()
        {
            var saveData = new TrainPlatformFluidContainerSaveData(Container);
            return MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(saveData));
        }

        private void LoadFluidToTrain(TrainCar dockedCar, FluidTrainCarContainer trainContainer)
        {
            if (Container == null || Container.Container.Amount < double.Epsilon)
            {
                _dockingComponent.StartRetracting();
                return;
            }

            _dockingComponent.StartExtending();
            if (_dockingComponent.ArmState != ArmState.Extended) return;

            if (trainContainer == null)
            {
                dockedCar.SetContainer(Container);
                Container = null;
                _dockingComponent.StartRetracting();
                return;
            }
            
            TransferFluid(Container.Container, trainContainer.Container);
            _dockingComponent.StartRetracting();
        }

        private void UnloadFluidFromTrain(TrainCar dockedCar, FluidTrainCarContainer trainContainer)
        {
            if (trainContainer == null || trainContainer.Container.Amount < double.Epsilon)
            {
                _dockingComponent.StartRetracting();
                return;
            }

            _dockingComponent.StartExtending();
            if (_dockingComponent.ArmState != ArmState.Extended) return;
            
            if (Container == null)
            {
                Container = trainContainer;
                dockedCar.SetContainer(null);
                _dockingComponent.StartRetracting();
                return;
            }
            
            TransferFluid(trainContainer.Container, Container.Container);
            _dockingComponent.StartRetracting();
        }

        private static void TransferFluid(FluidContainer from, FluidContainer to)
        {
            if (from.Amount < double.Epsilon) return;
            if (to.FluidId != FluidMaster.EmptyFluidId && to.FluidId != from.FluidId) return;

            var transferAmount = Math.Min(from.Amount, to.Capacity - to.Amount);
            if (transferAmount < double.Epsilon) return;

            if (to.FluidId == FluidMaster.EmptyFluidId)
            {
                to.FluidId = from.FluidId;
            }

            to.Amount += transferAmount;
            from.Amount -= transferAmount;

            if (from.Amount < double.Epsilon)
            {
                from.FluidId = FluidMaster.EmptyFluidId;
            }
        }

        private bool IsTargetContainer([CanBeNull] out FluidTrainCarContainer fluidContainer)
        {
            var trainContainer = _dockingComponent.DockedTrainCar?.Container;
            if (trainContainer is FluidTrainCarContainer f)
            {
                fluidContainer = f;
                return true;
            }

            if (trainContainer is null)
            {
                fluidContainer = null;
                return true;
            }

            fluidContainer = null;
            return false;
        }

        private void PushFluidToAdjacentBlocks()
        {
            if (Container == null) return;

            var fluidContainer = Container.Container;
            if (fluidContainer.Amount < double.Epsilon) return;

            foreach (var (inventory, info) in _fluidConnector.ConnectedTargets)
            {
                if (fluidContainer.Amount < double.Epsilon) break;

                var flowRate = GetFlowRate(info);
                var transferAmount = Math.Min(fluidContainer.Amount, flowRate * GameUpdater.SecondsPerTick);
                if (transferAmount < double.Epsilon) continue;

                var fluidStack = new FluidStack(transferAmount, fluidContainer.FluidId);
                var remain = inventory.AddLiquid(fluidStack, fluidContainer);
                var transferred = transferAmount - remain.Amount;
                if (transferred > 0)
                {
                    fluidContainer.Amount -= transferred;
                }
            }

            if (fluidContainer.Amount < double.Epsilon)
            {
                fluidContainer.FluidId = FluidMaster.EmptyFluidId;
            }

            fluidContainer.PreviousSourceFluidContainers.Clear();
        }

        private static double GetFlowRate(ConnectedInfo info)
        {
            if (info.SelfConnector?.ConnectOption is FluidConnectOption fluidOption)
            {
                return fluidOption.FlowCapacity;
            }
            throw new ArgumentException("FluidConnectOption is not set on connector");
        }

        [MessagePackObject]
        public class TrainPlatformFluidContainerSaveData
        {
            [Key(0)] public FluidTrainCarContainer Container;

            [Obsolete]
            public TrainPlatformFluidContainerSaveData() { }
            
            public TrainPlatformFluidContainerSaveData(FluidTrainCarContainer container)
            {
                Container = container;
            }
        }
    }
}

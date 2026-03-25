using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Fluid;
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
        private readonly FluidContainer _fluidContainer;

        public TrainPlatformFluidContainerComponent(
            TrainPlatformDockingComponent dockingComponent,
            TrainPlatformTransferComponent transferComponent,
            double capacity,
            BlockConnectorComponent<IFluidInventory> fluidConnector)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _fluidConnector = fluidConnector;
            _fluidContainer = new FluidContainer(capacity);
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
            _fluidContainer = new FluidContainer(capacity);

            if (componentStates.TryGetValue(SaveKey, out var serialized))
            {
                var serializedBytes = MessagePackSerializer.ConvertFromJson(serialized);
                var saveData = MessagePackSerializer.Deserialize<TrainPlatformFluidContainerSaveData>(serializedBytes);
                _fluidContainer.FluidId = saveData.FluidContainer.FluidId;
                _fluidContainer.Amount = saveData.FluidContainer.Amount;
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
            var result = new List<FluidStack>();
            if (_fluidContainer.Amount > 0)
            {
                result.Add(new FluidStack(_fluidContainer.Amount, _fluidContainer.FluidId));
            }
            return result;
        }

        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            return _fluidContainer.AddLiquid(fluidStack, source);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

        public string SaveKey { get; } = typeof(TrainPlatformFluidContainerComponent).FullName;

        public string GetSaveState()
        {
            var saveData = new TrainPlatformFluidContainerSaveData(_fluidContainer);
            return MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(saveData));
        }

        private void LoadFluidToTrain(TrainCar dockedCar, FluidTrainCarContainer trainContainer)
        {
            if (_fluidContainer.Amount < double.Epsilon)
            {
                _dockingComponent.StartRetracting();
                return;
            }

            _dockingComponent.StartExtending();
            if (_dockingComponent.ArmState != ArmState.Extended) return;

            if (trainContainer == null)
            {
                var newContainer = new FluidTrainCarContainer(new FluidContainer(_fluidContainer.Capacity));
                TransferFluid(_fluidContainer, newContainer.Container);
                dockedCar.SetContainer(newContainer);
                Container = newContainer;
                _dockingComponent.StartRetracting();
                return;
            }

            TransferFluid(_fluidContainer, trainContainer.Container);
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

            TransferFluid(trainContainer.Container, _fluidContainer);
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
                Container = f;
                return true;
            }

            if (trainContainer is null)
            {
                fluidContainer = null;
                Container = null;
                return true;
            }

            fluidContainer = null;
            return false;
        }

        private void PushFluidToAdjacentBlocks()
        {
            if (_fluidContainer.Amount < double.Epsilon) return;

            foreach (var kvp in _fluidConnector.ConnectedTargets)
            {
                if (_fluidContainer.Amount < double.Epsilon) break;

                var fluidStack = new FluidStack(_fluidContainer.Amount, _fluidContainer.FluidId);
                var remain = kvp.Key.AddLiquid(fluidStack, _fluidContainer);
                _fluidContainer.Amount -= (fluidStack.Amount - remain.Amount);
            }

            if (_fluidContainer.Amount < double.Epsilon)
            {
                _fluidContainer.FluidId = FluidMaster.EmptyFluidId;
            }

            _fluidContainer.PreviousSourceFluidContainers.Clear();
        }

        [MessagePackObject]
        public class TrainPlatformFluidContainerSaveData
        {
            [Key(0)] public FluidContainer FluidContainer;

            [Obsolete]
            public TrainPlatformFluidContainerSaveData() { }

            public TrainPlatformFluidContainerSaveData(FluidContainer fluidContainer)
            {
                FluidContainer = fluidContainer;
            }
        }
    }
}

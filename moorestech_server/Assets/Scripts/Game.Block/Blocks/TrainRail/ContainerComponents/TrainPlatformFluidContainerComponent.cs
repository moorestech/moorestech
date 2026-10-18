using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Fluid;
using Game.Train.Event;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using JetBrains.Annotations;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.State;
using MessagePack;
using UniRx;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    public class TrainPlatformFluidContainerComponent : IUpdatableBlockComponent, IFluidInventory, IBlockSaveState, IBlockStateObservable
    {
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        private readonly Subject<Unit> _onChangeBlockState = new();

        public bool IsDestroy { get; private set; }
        [CanBeNull] public FluidTrainCarContainer Container { get; private set; }

        private readonly TrainPlatformDockingComponent _dockingComponent;
        private readonly TrainPlatformTransferComponent _transferComponent;
        private readonly BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>> _fluidConnector;
        private readonly double _capacity;

        public TrainPlatformFluidContainerComponent(
            TrainPlatformDockingComponent dockingComponent,
            TrainPlatformTransferComponent transferComponent,
            double capacity,
            BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>> fluidConnector)
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
            BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>> fluidConnector,
            Dictionary<string, object> componentStates)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _fluidConnector = fluidConnector;
            _capacity = capacity;

            if (BlockComponentStateReader.TryRead<TrainPlatformFluidContainerSaveJsonObject>(componentStates, SaveKey, out var saveData))
            {
                if (saveData.Fluid != null)
                {
                    Container = new FluidTrainCarContainer(saveData.Fluid.ToFluidContainer(_capacity));
                }
            }
        }

        public void Update()
        {
            // 液体を移した操作で通知する
            // Notify directly after moving fluid
            if (Container != null && 0 < TrainPlatformFluidTransfer.PushToAdjacent(Container.Container, _fluidConnector)) NotifyFluidChanged();

            var dockedCar = _dockingComponent.DockedTrainCar;
            if (dockedCar == null) return; // silent-ok: 未停車は通常状態 / PF表示で確認

            if (!TrainPlatformFluidTransfer.IsTargetContainer(dockedCar, Container, out var targetContainer)) return; // silent-ok: 非対象貨車は通常状態 / 貨車表示で確認

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

        public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
        {
            if (Container == null) Container = new FluidTrainCarContainer(new FluidContainer(_capacity));

            var result = Container.Container.AddLiquid(fluidStack);
            if (0 < result.AcceptedAmount) NotifyFluidChanged();
            return result.Remainder;
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            // PF自身の液体を出力タンクとして配信する
            // Publish the platform's own fluid as an output tank
            var tank = Container == null
                ? new FluidMessagePack(FluidMaster.EmptyFluidId, 0, _capacity)
                : new FluidMessagePack(Container.Container.FluidId, Container.Container.Amount, Container.Container.Capacity);
            var detail = new FluidMachineInventoryStateDetail(new List<FluidMessagePack>(), new List<FluidMessagePack> { tank });
            return new[] { new BlockStateDetail(FluidMachineInventoryStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail)) };
        }

        private void NotifyFluidChanged()
        {
            _onChangeBlockState.OnNext(Unit.Default);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

        public string SaveKey { get; } = typeof(TrainPlatformFluidContainerComponent).FullName;

        public object GetSaveState()
        {
            // コンテナ未生成時はnull、ある時は内部FluidContainerをGUIDで保存する
            // Persist null when no container exists; otherwise persist the inner FluidContainer by GUID
            var fluid = Container != null ? new FluidContainerSaveJsonObject(Container.Container) : null;
            return new TrainPlatformFluidContainerSaveJsonObject { Fluid = fluid };
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
                NotifyFluidChanged();
                ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>().NotifySnapshotByCar(dockedCar);
                _dockingComponent.StartRetracting();
                return;
            }
            
            if (0 < TrainPlatformFluidTransfer.Transfer(Container.Container, trainContainer.Container)) NotifyFluidChanged();
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
                NotifyFluidChanged();
                dockedCar.SetContainer(null);
                ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>().NotifySnapshotByCar(dockedCar);
                _dockingComponent.StartRetracting();
                return;
            }

            if (0 < TrainPlatformFluidTransfer.Transfer(trainContainer.Container, Container.Container)) NotifyFluidChanged();
            _dockingComponent.StartRetracting();
        }

    }
}

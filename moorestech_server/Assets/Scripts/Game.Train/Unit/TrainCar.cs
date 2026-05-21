using System;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.Train.Diagram;
using Game.Train.Event;
using Game.Train.SaveLoad;
using Game.Train.Unit.Containers;
using JetBrains.Annotations;
using MessagePack;
using Mooresmaster.Model.TrainModule;
using UnityEngine;
using FluidContainer = Game.Fluid.FluidContainer;

namespace Game.Train.Unit
{
    /// <summary>
    /// 列車編成を構成する1両を表すクラス
    /// Represents a single car within a train formation.
    /// </summary>
    public class TrainCar : ITrainDiagramCar
    {
        private readonly TrainCarInstanceId _trainCarInstanceId;
        
        // 列車のマスターデータ
        public TrainCarMasterElement TrainCarMasterElement { get; }
        
        // 駆動力 (動力車での推進力、貨車では0)
        public int TractionForce { get; private set; }
        
        [CanBeNull] public ITrainCarContainer Container { get; private set; }

        //列車自体の長さ
        public int Length { get; private set; }
        //列車が駅とドッキングしているかどうか
        public bool IsDocked => dockingblock != null; // ドッキングしているかどうかのプロパティ
        public TrainCarInstanceId TrainCarInstanceId => _trainCarInstanceId;
        public IBlock dockingblock { get; set; }// このTrainCarがcargoやstation駅blockでドッキングしているときにのみ非nullになる。前輪を登録
        public bool IsFacingForward { get; private set; }
        public double RemainFuelTime { get; private set; }
        
        private readonly TrainUpdateEvent _trainUpdateEvent;

        // 新規車両用: インスタンスIDを新規採番する
        // For new cars: generates a fresh instance id.
        public TrainCar(TrainCarMasterElement trainCarMaster, bool isFacingForward)
            : this(trainCarMaster, isFacingForward, TrainCarInstanceId.Create())
        {
        }

        // セーブ復元用: 保存済みインスタンスIDを引き継ぐ
        // For save restore: carries over the persisted instance id.
        public TrainCar(TrainCarMasterElement trainCarMaster, bool isFacingForward, TrainCarInstanceId trainCarInstanceId)
        {
            _trainCarInstanceId = trainCarInstanceId;
            TrainCarMasterElement = trainCarMaster;
            TractionForce = trainCarMaster.TractionForce;
            Length = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
            IsFacingForward = isFacingForward;
            dockingblock = null;

            _trainUpdateEvent = (TrainUpdateEvent)ServerContext.GetService<ITrainUpdateEvent>();

            // マスタ指定のデフォルトコンテナを装着する(セーブ復元時はRestoreTrainCar内のSetContainerで上書きされる)
            // Attach the default container per master spec; RestoreTrainCar's SetContainer overrides it on load.
            AttachDefaultContainerFromMaster();

            #region Internal

            void AttachDefaultContainerFromMaster()
            {
                switch (trainCarMaster.DefaultContainerType)
                {
                    case "Item":
                        SetContainer(ItemTrainCarContainer.CreateWithEmptySlots(trainCarMaster.InventorySlots));
                        break;
                    case "Fluid":
                        SetContainer(new FluidTrainCarContainer(new FluidContainer(trainCarMaster.FluidCapacity)));
                        break;
                    // None または未指定はコンテナ無し
                    // None or unspecified leaves the car without a container.
                }
            }

            #endregion
        }
        
        public void ConsumeFuel(double time, int masconLevel)
        {
            if (RemainFuelTime <= 0) return;

            var normalizedMasconLevel = masconLevel / (double)MasterHolder.TrainUnitMaster.MasconLevelMaximum;
            RemainFuelTime -= time * Math.Abs(normalizedMasconLevel);
        }

        //重さ、推進力を得る
        public (int weight, int tractionForce) GetWeightAndTraction(int masconLevel)
        {
            var weight = TrainCarMasterElement.Weight + (Container?.GetWeight() ?? 0);
            if (RemainFuelTime <= 0)
            {
                if (masconLevel != 0 && Container is IFuelProviderTrainCarContainer fuelProviderTrainCarContainer) RemainFuelTime += fuelProviderTrainCarContainer.ConsumeFuel(this);
                if (RemainFuelTime <= 0) return (weight, 0);
            }
            
            var tractionForce = TractionForce;
            return (weight, tractionForce);
        }

        public void SetRemainFuelTime(double value)
        {
            RemainFuelTime = value;
        }

        public void SetFacingForward(bool isFacingForward)
        {
            IsFacingForward = isFacingForward;
        }
        public void Reverse()
        {
            IsFacingForward = !IsFacingForward;
        }
        
        public void SetContainer(ITrainCarContainer container)
        {
            Container?.OnDetachedFromCar();
            Container = container;
            Container?.OnAttachedToCar(this);
        }

        internal void NotifyInventoryUpdate(int slot, IItemStack itemStack)
        {
            _trainUpdateEvent.InvokeInventoryUpdate(new TrainInventoryUpdateEventProperties(_trainCarInstanceId, slot, itemStack));
        }

        public TrainCarSaveData CreateTrainCarSaveData()
        {
            SerializableVector3Int? dockingPosition = null;
            if (this.dockingblock != null)
            {
                var blockPosition = this.dockingblock.BlockPositionInfo.OriginalPos;
                dockingPosition = new SerializableVector3Int(blockPosition.x, blockPosition.y, blockPosition.z);
            }

            return new TrainCarSaveData
            {
                TrainCarMasterId = this.TrainCarMasterElement.TrainCarGuid,
                IsFacingForward = this.IsFacingForward,
                DockingBlockPosition = dockingPosition,
                ContainerSaveData = MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(Container)),
                RemainFuelTime = this.RemainFuelTime
            };
        }


        public static TrainCar RestoreTrainCar(TrainCarSaveData data)
        {
            if (data == null)
                return null;

            if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(data.TrainCarMasterId, out var trainCarMaster)) throw new Exception("trainCarMaster is not found");
            var isFacingForward = data.IsFacingForward;
            var car = new TrainCar(trainCarMaster, isFacingForward);

            if (data.DockingBlockPosition.HasValue)
            {
                var block = ServerContext.WorldBlockDatastore.GetBlock((Vector3Int)data.DockingBlockPosition.Value);
                if (block != null)
                {
                    car.dockingblock = block;
                }
            }
            
            // ロード時もSetContainer経由で通知バインドを行う
            // Restore via SetContainer so the notification binding is established on load.
            var restoredContainer = MessagePackSerializer.Deserialize<ITrainCarContainer>(MessagePackSerializer.ConvertFromJson(data.ContainerSaveData));
            car.SetContainer(restoredContainer);

            car.RemainFuelTime = data.RemainFuelTime;
            
            return car;
        }

        public void Destroy()
        {
            // 通知バインドを解除し、Container参照も切る
            // Release the notification binding and clear the container reference.
            SetContainer(null);
            _trainUpdateEvent.InvokeTrainCarRemoved(_trainCarInstanceId);
        }
        
        public bool IsInventoryFull()
        {
            return Container?.IsFull() ?? true;
        }
        public bool IsInventoryEmpty()
        {
            return Container?.IsEmpty() ?? true;
        }
    }
}

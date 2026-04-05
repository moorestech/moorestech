using System;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.Train.Diagram;
using Game.Train.Event;
using Game.Train.SaveLoad;
using JetBrains.Annotations;
using MessagePack;
using Mooresmaster.Model.TrainModule;
using UnityEngine;

namespace Game.Train.Unit
{
    /// <summary>
    /// 列車編成を構成する1両を表すクラス
    /// Represents a single car within a train formation.
    /// </summary>
    public class TrainCar : ITrainDiagramCar
    {
        private readonly TrainCarInstanceId _trainCarInstanceId = TrainCarInstanceId.Create();
        
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

        //TODO燃料スロット数削除について修正は今後
        public TrainCar(TrainCarMasterElement trainCarMaster, bool isFacingForward = true, int fuelSlots = 0)
        {
            TrainCarMasterElement = trainCarMaster;
            TractionForce = trainCarMaster.TractionForce;
            Length = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
            IsFacingForward = isFacingForward;
            dockingblock = null;
            Container = null;
            
            _trainUpdateEvent = (TrainUpdateEvent)ServerContext.GetService<ITrainUpdateEvent>();
        }
        
        public void ConsumeFuel(double time, int masconLevel)
        {
            if (RemainFuelTime <= 0) return;

            var normalizedMasconLevel = masconLevel / (double)TrainMotionParameters.MasconLevelMaximum;
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
            
            var tractionForce = IsFacingForward ? TractionForce * TrainCarMasterElement.TractionForce : 0;
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
            Container = container;
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
            
            car.Container = MessagePackSerializer.Deserialize<ITrainCarContainer>(MessagePackSerializer.ConvertFromJson(data.ContainerSaveData));

            car.RemainFuelTime = data.RemainFuelTime;
            
            return car;
        }

        public void Destroy()
        {
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

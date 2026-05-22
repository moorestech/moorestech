using System;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerRiding.Interface;
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
    public class TrainCar : ITrainDiagramCar, IRidable
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

        // IRidable 実装: 乗り物としての識別子と座席数を公開する
        // IRidable implementation: exposes the ridable identifier and seat count.
        IRidableIdentifier IRidable.Identifier => new TrainCarRidableIdentifier(_trainCarInstanceId.AsPrimitive());
        int IRidable.SeatCount => TrainCarMasterElement.RidableSeats?.Length ?? 0;

        public IBlock dockingblock { get; set; }// このTrainCarがcargoやstation駅blockでドッキングしているときにのみ非nullになる。前輪を登録
        public bool IsFacingForward { get; private set; }
        public double RemainFuelTime { get; private set; }
        public double CurrentFuelTimePerItem { get; private set; }
        public double RemainFuelRate => CurrentFuelTimePerItem <= 0 ? 0 : Clamp01(RemainFuelTime / CurrentFuelTimePerItem);// 消費燃料1個あたり何%分残りあるか
        
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
        
        // 加速時入力masconlevelから消費燃料を考慮しtrainunitが最後に使用するためのmasconlevelを算出するため
        public (double tractionForce, double baseTractionForce) ConsumeFuelAndResolveTraction(int masconLevel)
        {
            // 加速に制限が出る燃料ならここでmasconLevelをクランプするように
            if (TractionForce == 0) return (0, 0);
            return (ConsumeFuel(masconLevel) * TractionForce * masconLevel / MasterHolder.TrainUnitMaster.MasconLevelMaximum, TractionForce);
            
            // masconLevelの加速が適応される前提で先に燃料消費をする。残燃料しだいでmasconLevelの分消費できないことがあるので理想のうち何割消費したかを返す
            double ConsumeFuel(int masconLevel)
            {
                if (masconLevel <= 0) return 0.0;
                var normalizedMasconLevel = masconLevel / (double)MasterHolder.TrainUnitMaster.MasconLevelMaximum;
                //予定消費燃料数
                double baseConsume = GameUpdater.SecondsPerTick * normalizedMasconLevel;
                double fuelConsumed = baseConsume;
                while (fuelConsumed > 0.0)
                {
                    if (RemainFuelTime >= fuelConsumed)
                    {
                        RemainFuelTime -= fuelConsumed;
                        fuelConsumed = 0.0;
                        break;
                    }
                    fuelConsumed -= RemainFuelTime;
                    RemainFuelTime = 0.0;
                    LoadFuelItemIfEmpty();
                    if (RemainFuelTime == 0.0) break;
                }
                return (baseConsume - fuelConsumed) / baseConsume;
            }
        }
        public int GetWeight()
        {
            var weight = TrainCarMasterElement.Weight + (Container?.GetWeight() ?? 0);
            return weight;
        }
        
        public void SetRemainFuelTime(double value)
        {
            RemainFuelTime = Math.Max(0, value);
            CurrentFuelTimePerItem = RemainFuelTime;
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
                TrainCarInstanceId = this._trainCarInstanceId.AsPrimitive(),
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
            var car = new TrainCar(trainCarMaster, isFacingForward, new TrainCarInstanceId(data.TrainCarInstanceId));

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

            car.SetRemainFuelTime(data.RemainFuelTime);
            
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

        private void LoadFuelItemIfEmpty()
        {
            if (RemainFuelTime > 0) return;

            CurrentFuelTimePerItem = 0;
            if (Container is not IFuelProviderTrainCarContainer fuelProviderTrainCarContainer) return;

            // 燃料1個ぶんの総量を記録し、残量率を計算できる状態にする。
            // Store the total value of one fuel item so remaining percentage can be calculated.
            var fuelTime = fuelProviderTrainCarContainer.ConsumeFuel(this);
            if (fuelTime <= 0) return;
            RemainFuelTime = fuelTime;
            CurrentFuelTimePerItem = fuelTime;
        }

        private static double Clamp01(double value)
        {
            if (value <= 0) return 0;
            if (value >= 1) return 1;
            return value;
        }
    }
}

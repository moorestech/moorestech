using System;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Newtonsoft.Json;

namespace Game.Train.SaveLoad
{
    internal static class TrainCarSaveDataConverter
    {
        public static TrainCarSaveData Create(TrainCar car)
        {
            // 保存フィールドを変えず車両状態を写す
            // Copy the car state without changing saved fields
            SerializableVector3Int? dockingPosition = null;
            if (car.dockingblock != null)
            {
                var blockPosition = car.dockingblock.BlockPositionInfo.OriginalPos;
                dockingPosition = new SerializableVector3Int(blockPosition.x, blockPosition.y, blockPosition.z);
            }

            return new TrainCarSaveData
            {
                TrainCarInstanceId = car.TrainCarInstanceId.AsPrimitive(),
                TrainCarMasterId = car.TrainCarMasterElement.TrainCarGuid,
                IsFacingForward = car.IsFacingForward,
                DockingBlockPosition = dockingPosition,
                ContainerSaveData = JsonConvert.SerializeObject(TrainCarContainerSaveJsonObject.FromContainer(car.Container)),
                RemainFuelTime = car.RemainFuelTime
            };
        }


        public static TrainCar Restore(TrainCarSaveData data)
        {
            if (data == null)
            {
                UnityEngine.Debug.LogWarning("TrainCar restore skipped: save data is null");
                return null;
            }

            if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(data.TrainCarMasterId, out var trainCarMaster)) throw new Exception("trainCarMaster is not found");
            var isFacingForward = data.IsFacingForward;
            var car = new TrainCar(trainCarMaster, isFacingForward, new TrainCarInstanceId(data.TrainCarInstanceId));

            if (data.DockingBlockPosition.HasValue)
            {
                var block = ServerContext.WorldBlockDatastore.GetBlock(data.DockingBlockPosition.Value);
                if (block != null)
                {
                    car.dockingblock = block;
                }
                else UnityEngine.Debug.LogWarning($"TrainCar restore: docking block not found at {data.DockingBlockPosition.Value}");
            }

            // ロード時もSetContainer経由で通知バインドを行う
            // Restore via SetContainer so the notification binding is established on load.
            var restoredContainer = JsonConvert.DeserializeObject<TrainCarContainerSaveJsonObject>(data.ContainerSaveData)?.ToContainer(trainCarMaster);
            car.SetContainer(restoredContainer);

            car.SetRemainFuelTime(data.RemainFuelTime);

            return car;
        }

    }
}

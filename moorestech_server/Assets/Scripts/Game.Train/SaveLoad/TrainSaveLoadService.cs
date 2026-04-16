using System.Collections.Generic;
using Game.Train.Unit;

namespace Game.Train.SaveLoad
{
    public class TrainSaveLoadService
    {
        private readonly TrainUnitDatastore _trainUnitDatastore;

        public TrainSaveLoadService(TrainUnitDatastore trainUnitDatastore)
        {
            _trainUnitDatastore = trainUnitDatastore;
        }
        public List<TrainUnitSaveData> GetSaveJsonObject()
        {
            var saveData = new List<TrainUnitSaveData>();
            foreach (var train in _trainUnitDatastore.GetRegisteredTrains())
            {
                if (train == null)
                {
                    continue;
                }
                saveData.Add(train.CreateSaveData());
            }

            return saveData;
        }

        public void RestoreTrainStates(IEnumerable<TrainUnitSaveData> saveData)
        {
            // Save/Loadサイクルのたびに登録済み列車を初期化して、
            // Reset registered trains for each save/load cycle.
            // 既存状態が残ったまま復元処理が走るのを防ぐ。
            // Prevent stale train state from surviving into restore.
            _trainUnitDatastore.Reset();

            if (saveData == null)
            {
                return;
            }

            // 復元したTrainUnitをDatastoreへ戻して、以後の参照系が通常どおり動くようにする。
            // Register restored TrainUnits so lookup and save flows can see them again.
            foreach (var data in saveData)
            {
                if (data == null)
                    continue;
                var trainUnit = TrainUnit.RestoreFromSaveData(data);
                if (trainUnit == null)
                    continue;
                if (trainUnit.Cars == null || trainUnit.Cars.Count == 0)
                    continue;
                _trainUnitDatastore.RegisterTrain(trainUnit);
            }
        }
    }
}


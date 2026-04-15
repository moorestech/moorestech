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
            // 既存状態が残ったまま復元処理が走るのを防ぐ。
            _trainUnitDatastore.Clear();

            if (saveData == null)
            {
                return;
            }

            foreach (var data in saveData)
            {
                if (data == null)
                {
                    continue;
                }

                TrainUnit.RestoreFromSaveData(data);
            }
        }
    }
}


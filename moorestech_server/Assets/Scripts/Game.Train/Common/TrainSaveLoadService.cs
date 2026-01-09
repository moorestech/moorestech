using System.Collections.Generic;
using Game.Train.Train;

namespace Game.Train.Common
{
    public class TrainSaveLoadService
    {
        private readonly TrainUpdateService _trainUpdateService;

        public TrainSaveLoadService(TrainUpdateService trainUpdateService)
        {
            _trainUpdateService = trainUpdateService;
        }
        public List<TrainUnitSaveData> GetSaveJsonObject()
        {
            var saveData = new List<TrainUnitSaveData>();
            foreach (var train in _trainUpdateService.GetRegisteredTrains())
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
            _trainUpdateService.ResetTrains();

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


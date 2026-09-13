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
            // Dictionaryの列挙順は削除跡の再利用で変わる。添字位置で突き合わせる比較器のため保存側で正準化する
            // Dictionary order shifts as removed slots get reused, so canonicalize here for comparers that match by index
            var trains = new List<TrainUnit>();
            foreach (var train in _trainUnitDatastore.GetRegisteredTrains())
            {
                if (train == null)
                {
                    continue;
                }
                trains.Add(train);
            }
            trains.Sort((left, right) => left.TrainUnitInstanceId.AsPrimitive().CompareTo(right.TrainUnitInstanceId.AsPrimitive()));

            var saveData = new List<TrainUnitSaveData>();
            foreach (var train in trains) saveData.Add(train.CreateSaveData());

            return saveData;
        }

        public void RestoreTrainStates(IEnumerable<TrainUnitSaveData> saveData)
        {
            // Save/Loadサイクルのたびに登録済み列車を初期化して、
            // 既存状態が残ったまま復元処理が走るのを防ぐ。
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
                _trainUnitDatastore.RegisterTrain(trainUnit);
            }
        }
    }
}


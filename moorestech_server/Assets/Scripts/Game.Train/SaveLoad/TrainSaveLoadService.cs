using System.Collections.Generic;
using Game.Train.Unit;
using UnityEngine;

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
                // マスタ欠損の除去器が復元不能な列車を事前に外すので通常は来ない。来たら次のautosaveで消えるため理由を残す
                // The missing-master pruner removes unrestorable trains beforehand, so this is abnormal; it vanishes on the next autosave, hence the log
                if (data == null)
                {
                    Debug.LogWarning("セーブのtrainUnitsにnullの列車があるため復元せず読み飛ばします。次のセーブで消えます。");
                    continue;
                }
                var trainUnit = TrainUnit.RestoreFromSaveData(data);
                if (trainUnit == null)
                {
                    Debug.LogWarning($"列車のレール位置を解決できないため復元せず読み飛ばします。次のセーブで貨車と積荷ごと消えます。 trainUnitInstanceId={data.TrainUnitInstanceId} cars={data.Cars?.Count}");
                    continue;
                }
                _trainUnitDatastore.RegisterTrain(trainUnit);
            }
        }
    }
}


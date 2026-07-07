using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;
using UnityEngine;

namespace Game.UnlockState.Holders
{
    public class TrainCarUnlockStateHolder
    {
        public IObservable<Guid> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> Infos => _infos;

        private readonly Subject<Guid> _onUnlock = new();
        private readonly Dictionary<Guid, TrainCarUnlockStateInfo> _infos = new();

        public TrainCarUnlockStateHolder()
        {
            foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
            {
                if (_infos.ContainsKey(trainCar.TrainCarGuid)) continue;
                // InitialUnlockedはoptionalスキーマのためnull=falseとして扱う
                // InitialUnlocked is an optional schema field; treat null as false
                _infos.Add(trainCar.TrainCarGuid, new TrainCarUnlockStateInfo(trainCar.TrainCarGuid, trainCar.InitialUnlocked ?? false));
            }
        }

        public void Unlock(Guid trainCarGuid)
        {
            if (!_infos.ContainsKey(trainCarGuid))
            {
                Debug.LogError($"[UnlockTrainCar] Train car not found: {trainCarGuid}");
                return;
            }
            _infos[trainCarGuid].Unlock();
            _onUnlock.OnNext(trainCarGuid);
        }

        public void Load(List<TrainCarUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                // マスタに存在しない車両はスキップ
                // Skip train cars that don't exist in master
                if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(Guid.Parse(jsonObject.TrainCarGuid), out _)) continue;
                var state = new TrainCarUnlockStateInfo(jsonObject);
                _infos[state.TrainCarGuid] = state;
            }
        }

        public List<TrainCarUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new TrainCarUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;
using UnityEngine;

namespace Game.UnlockState.Holders
{
    public class ChallengeCategoryUnlockStateHolder
    {
        public IObservable<Guid> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> Infos => _infos;

        private readonly Subject<Guid> _onUnlock = new();
        private readonly Dictionary<Guid, ChallengeCategoryUnlockStateInfo> _infos = new();

        public ChallengeCategoryUnlockStateHolder()
        {
            foreach (var challenge in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements)
            {
                if (_infos.ContainsKey(challenge.CategoryGuid)) continue;
                _infos.Add(challenge.CategoryGuid, new ChallengeCategoryUnlockStateInfo(challenge.CategoryGuid, challenge.InitialUnlocked));
            }
        }

        public void Unlock(Guid categoryGuid)
        {
            if (!_infos.ContainsKey(categoryGuid))
            {
                Debug.LogError($"[UnlockChallenge] Challenge category not found: {categoryGuid}");
                return;
            }
            _infos[categoryGuid].Unlock();
            _onUnlock.OnNext(categoryGuid);
        }

        public void Load(List<ChallengeUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                var state = new ChallengeCategoryUnlockStateInfo(jsonObject);
                _infos[state.ChallengeCategoryGuid] = state;
            }
        }

        public List<ChallengeUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new ChallengeUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}

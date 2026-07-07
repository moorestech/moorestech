using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;
using UnityEngine;

namespace Game.UnlockState.Holders
{
    public class MachineRecipeUnlockStateHolder
    {
        public IObservable<Guid> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> Infos => _infos;

        private readonly Subject<Guid> _onUnlock = new();
        private readonly Dictionary<Guid, MachineRecipeUnlockStateInfo> _infos = new();

        public MachineRecipeUnlockStateHolder()
        {
            foreach (var machineRecipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (_infos.ContainsKey(machineRecipe.MachineRecipeGuid)) continue;
                _infos.Add(machineRecipe.MachineRecipeGuid, new MachineRecipeUnlockStateInfo(machineRecipe.MachineRecipeGuid, machineRecipe.InitialUnlocked));
            }
        }

        public void Unlock(Guid machineRecipeGuid)
        {
            if (!_infos.ContainsKey(machineRecipeGuid))
            {
                Debug.LogError($"[UnlockMachineRecipe] Machine recipe not found: {machineRecipeGuid}");
                return;
            }
            _infos[machineRecipeGuid].Unlock();
            _onUnlock.OnNext(machineRecipeGuid);
        }

        public void Load(List<MachineRecipeUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                var state = new MachineRecipeUnlockStateInfo(jsonObject);
                _infos[state.MachineRecipeGuid] = state;
            }
        }

        public List<MachineRecipeUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new MachineRecipeUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}

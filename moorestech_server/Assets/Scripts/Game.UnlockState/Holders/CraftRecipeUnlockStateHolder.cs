using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;

namespace Game.UnlockState.Holders
{
    public class CraftRecipeUnlockStateHolder
    {
        public IObservable<Guid> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> Infos => _infos;

        private readonly Subject<Guid> _onUnlock = new();
        private readonly Dictionary<Guid, CraftRecipeUnlockStateInfo> _infos = new();

        public CraftRecipeUnlockStateHolder()
        {
            // 初期解放フラグ付きで登録
            // Register all craft recipes with their initial unlocked flag
            foreach (var recipe in MasterHolder.CraftRecipeMaster.GetAllCraftRecipes())
            {
                if (_infos.ContainsKey(recipe.CraftRecipeGuid)) continue;
                _infos.Add(recipe.CraftRecipeGuid, new CraftRecipeUnlockStateInfo(recipe.CraftRecipeGuid, recipe.InitialUnlocked));
            }
        }

        public void Unlock(Guid recipeGuid)
        {
            _infos[recipeGuid].Unlock();
            _onUnlock.OnNext(recipeGuid);
        }

        public void Load(List<CraftRecipeUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                var state = new CraftRecipeUnlockStateInfo(jsonObject);
                _infos[state.CraftRecipeGuid] = state;
            }
        }

        public List<CraftRecipeUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new CraftRecipeUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}

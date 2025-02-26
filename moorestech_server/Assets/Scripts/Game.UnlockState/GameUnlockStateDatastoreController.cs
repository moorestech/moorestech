using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using Newtonsoft.Json;
using UniRx;

namespace Game.UnlockState
{
    public class GameUnlockStateDataController : IGameUnlockStateDataController
    {
        public GameUnlockStateDataController()
        {
            var recipes = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes();
            foreach (var recipe in recipes)
            {
                var guid = recipe.CraftRecipeGuid;
                if (!CraftRecipeUnlockStateInfos.ContainsKey(guid))
                {
                    _recipeUnlockStateInfos.Add(guid, new CraftRecipeUnlockStateInfo(guid, recipe.InitialUnlocked ?? false));
                }
            }
        }
        
        
        public IObservable<Guid> OnUnlockCraftRecipe => _onUnlockRecipe;
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos => _recipeUnlockStateInfos;
        
        private readonly Subject<Guid> _onUnlockRecipe = new();
        private readonly Dictionary<Guid, CraftRecipeUnlockStateInfo> _recipeUnlockStateInfos = new();
        public void UnlockCraftRecipe(Guid recipeGuid)
        {
            CraftRecipeUnlockStateInfos[recipeGuid].Unlock();
            _onUnlockRecipe.OnNext(recipeGuid);
        }
        
        
        public IObservable<ItemId> OnUnlockItem => _onUnlockItem;
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos => _itemUnlockStateInfos;
        
        private readonly Subject<ItemId> _onUnlockItem = new();
        private readonly Dictionary<ItemId, ItemUnlockStateInfo> _itemUnlockStateInfos = new();
        public void UnlockItem(ItemId itemId)
        {
            ItemUnlockStateInfos[itemId].Unlock();
            _onUnlockItem.OnNext(itemId);
        }
        
        
        #region SaveLoad
        
        public void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject)
        {
            LoadRecipeUnlockStateInfos();
            
            #region Internal
            
            void LoadRecipeUnlockStateInfos()
            {
                foreach (var recipeUnlockStateInfo in stateJsonObject.CraftRecipeUnlockStateInfos)
                {
                    var recipeGuid = Guid.Parse(recipeUnlockStateInfo.CraftRecipeGuid);
                    _recipeUnlockStateInfos[recipeGuid] = new CraftRecipeUnlockStateInfo(recipeUnlockStateInfo);
                }
            }
            
            #endregion
        }
        
        public GameUnlockStateJsonObject GetSaveJsonObject()
        {
            var recipeUnlockStateInfos = CraftRecipeUnlockStateInfos.Values.Select(r => new CraftRecipeUnlockStateInfoJsonObject(r)).ToList();
            var itemUnlockStateInfos = ItemUnlockStateInfos.Values.Select(i => new ItemUnlockStateInfoJsonObject(i)).ToList();
            return new GameUnlockStateJsonObject
            {
                CraftRecipeUnlockStateInfos = recipeUnlockStateInfos,
                ItemUnlockStateInfos = itemUnlockStateInfos,
            };
        }
        
        #endregion
    }
    
    public class GameUnlockStateJsonObject
    {
        [JsonProperty("craftRecipeUnlockStateInfos")] public List<CraftRecipeUnlockStateInfoJsonObject> CraftRecipeUnlockStateInfos;
        [JsonProperty("itemUnlockStateInfos")] public List<ItemUnlockStateInfoJsonObject> ItemUnlockStateInfos;
    }
}
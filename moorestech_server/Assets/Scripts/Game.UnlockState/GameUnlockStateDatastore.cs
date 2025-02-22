using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Newtonsoft.Json;
using UniRx;

namespace Game.UnlockState
{
    public interface IGameUnlockStateDatastore
    {
        public IObservable<Guid> OnUnlockCraftRecipe { get; }
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; }
        void UnlockCraftRecipe(Guid recipeGuid);
        
        void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject);
        GameUnlockStateJsonObject GetSaveJsonObject();
    }
    
    public class GameUnlockStateDatastore : IGameUnlockStateDatastore
    {
        public GameUnlockStateDatastore()
        {
            var recipes = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes();
            foreach (var recipe in recipes)
            {
                var guid = recipe.CraftRecipeGuid;
                if (!CraftRecipeUnlockStateInfos.ContainsKey(guid))
                {
                    _recipeUnlockStateInfos.Add(guid, new CraftRecipeUnlockStateInfo(guid, recipe.InitialUnlocked));
                }
            }
        }
        
        
        public IObservable<Guid> OnUnlockCraftRecipe => _onUnlockRecipe;
        private readonly Subject<Guid> _onUnlockRecipe = new();
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos => _recipeUnlockStateInfos;
        private readonly Dictionary<Guid, CraftRecipeUnlockStateInfo> _recipeUnlockStateInfos = new();
        public void UnlockCraftRecipe(Guid recipeGuid)
        {
            CraftRecipeUnlockStateInfos[recipeGuid].Unlock(); 
        }
        
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
            return new GameUnlockStateJsonObject
            {
                CraftRecipeUnlockStateInfos = recipeUnlockStateInfos
            };
        }
    }
    
    public class GameUnlockStateJsonObject
    {
        [JsonProperty("craftRecipeUnlockStateInfos")] public List<CraftRecipeUnlockStateInfoJsonObject> CraftRecipeUnlockStateInfos;
    }
}
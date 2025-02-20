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
        public IObservable<Guid> OnUnlockRecipe { get; }
        void UnlockRecipe(Guid recipeGuid);
        
        void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject);
        GameUnlockStateJsonObject GetSaveJsonObject();
    }
    
    public class GameUnlockStateDatastore : IGameUnlockStateDatastore
    {
        public readonly Dictionary<Guid, RecipeUnlockStateInfo> RecipeUnlockStateInfos = new();
        private readonly Subject<Guid> _onUnlockRecipe = new();
        
        
        public IObservable<Guid> OnUnlockRecipe => _onUnlockRecipe;
        public void UnlockRecipe(Guid recipeGuid)
        {
            RecipeUnlockStateInfos[recipeGuid].Unlock(); 
        }
        
        
        public void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject)
        {
            LoadRecipeUnlockStateInfos();
            
            #region Internal
            
            void LoadRecipeUnlockStateInfos()
            {
                foreach (var recipeUnlockStateInfo in stateJsonObject.RecipeUnlockStateInfos)
                {
                    var recipeGuid = Guid.Parse(recipeUnlockStateInfo.RecipeGuid);
                    RecipeUnlockStateInfos.Add(recipeGuid, new RecipeUnlockStateInfo(recipeUnlockStateInfo));
                }
                
                var recipes = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes();
                foreach (var recipe in recipes)
                {
                    var guid = recipe.CraftRecipeGuid;
                    if (!RecipeUnlockStateInfos.ContainsKey(guid))
                    {
                        RecipeUnlockStateInfos.Add(guid, new RecipeUnlockStateInfo(guid, recipe.InitialUnlocked));
                    }
                }
            }
            
            #endregion
        }
        
        public GameUnlockStateJsonObject GetSaveJsonObject()
        {
            var recipeUnlockStateInfos = RecipeUnlockStateInfos.Values.Select(r => r.GetSaveJsonObject()).ToList();
            return new GameUnlockStateJsonObject
            {
                RecipeUnlockStateInfos = recipeUnlockStateInfos
            };
        }
    }
    
    public class GameUnlockStateJsonObject
    {
        [JsonProperty("recipeUnlockStateInfos")] public List<RecipeUnlockStateInfoJsonObject> RecipeUnlockStateInfos;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Newtonsoft.Json;

namespace Game.UnlockState
{
    public class GameUnlockStateDatastore
    {
        public readonly Dictionary<Guid, RecipeUnlockStateInfo> RecipeUnlockStateInfos = new();
        
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
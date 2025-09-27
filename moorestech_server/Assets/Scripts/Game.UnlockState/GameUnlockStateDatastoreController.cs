using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;

namespace Game.UnlockState
{
    public class GameUnlockStateDataController : IGameUnlockStateDataController
    {
        public GameUnlockStateDataController()
        {
            // Initialize recipe unlock states
            var recipes = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes();
            foreach (var recipe in recipes)
            {
                var guid = recipe.CraftRecipeGuid;
                if (!CraftRecipeUnlockStateInfos.ContainsKey(guid))
                {
                    _recipeUnlockStateInfos.Add(guid, new CraftRecipeUnlockStateInfo(guid, recipe.InitialUnlocked));
                }
            }
            
            // Initialize item unlock states
            var items = MasterHolder.ItemMaster.GetItemAllIds();
            foreach (var item in items)
            {
                if (!ItemUnlockStateInfos.ContainsKey(item))
                {
                    var itemMaster = MasterHolder.ItemMaster.GetItemMaster(item);
                    _itemUnlockStateInfos.Add(item, new ItemUnlockStateInfo(item, itemMaster.InitialUnlocked));
                }
            }

            // Initialize challenge unlock states
            foreach (var challenge in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements)
            {
                var guid = challenge.CategoryGuid;
                if (!ChallengeCategoryUnlockStateInfos.ContainsKey(guid))
                {
                    _challengeCategoryUnlockStateInfos.Add(guid, new ChallengeCategoryUnlockStateInfo(guid, challenge.InitialUnlocked));
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
        

        public IObservable<Guid> OnUnlockChallengeCategory => _onUnlockChallengeCategory;
        public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos => _challengeCategoryUnlockStateInfos;

        private readonly Subject<Guid> _onUnlockChallengeCategory = new();
        private readonly Dictionary<Guid, ChallengeCategoryUnlockStateInfo> _challengeCategoryUnlockStateInfos = new();
        public void UnlockChallenge(Guid categoryGuid)
        {
            if (!ChallengeCategoryUnlockStateInfos.ContainsKey(categoryGuid))
            {
                Debug.LogError($"[UnlockChallenge] Challenge category not found: {categoryGuid}");
                return;
            }
            ChallengeCategoryUnlockStateInfos[categoryGuid].Unlock();
            _onUnlockChallengeCategory.OnNext(categoryGuid);
        }
        
        
        #region SaveLoad
        
        public void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject)
        {
            LoadRecipeUnlockStateInfos();
            LoadItemUnlockStateInfos();
            LoadChallengeCategoryUnlockStateInfos();
            
            #region Internal
            
            void LoadRecipeUnlockStateInfos()
            {
                foreach (var recipeUnlockStateInfo in stateJsonObject.CraftRecipeUnlockStateInfos)
                {
                    var recipeGuid = Guid.Parse(recipeUnlockStateInfo.CraftRecipeGuid);
                    _recipeUnlockStateInfos[recipeGuid] = new CraftRecipeUnlockStateInfo(recipeUnlockStateInfo);
                }
            }
            
            void LoadItemUnlockStateInfos()
            {
                foreach (var itemUnlockStateInfo in stateJsonObject.ItemUnlockStateInfos)
                {
                    var state = new ItemUnlockStateInfo(itemUnlockStateInfo);
                    _itemUnlockStateInfos[state.ItemId] = state;
                }
            }

            void LoadChallengeCategoryUnlockStateInfos()
            {
                foreach (var challengeUnlockStateInfo in stateJsonObject.ChallengeCategoryUnlockStateInfos)
                {
                    var state = new ChallengeCategoryUnlockStateInfo(challengeUnlockStateInfo);
                    _challengeCategoryUnlockStateInfos[state.ChallengeCategoryGuid] = state;
                }
            }
            
            #endregion
        }
        
        public GameUnlockStateJsonObject GetSaveJsonObject()
        {
            var recipeUnlockStateInfos = CraftRecipeUnlockStateInfos.Values.Select(r => new CraftRecipeUnlockStateInfoJsonObject(r)).ToList();
            var itemUnlockStateInfos = ItemUnlockStateInfos.Values.Select(i => new ItemUnlockStateInfoJsonObject(i)).ToList();
            var challengeUnlockStateInfos = ChallengeCategoryUnlockStateInfos.Values.Select(c => new ChallengeUnlockStateInfoJsonObject(c)).ToList();
            return new GameUnlockStateJsonObject
            {
                CraftRecipeUnlockStateInfos = recipeUnlockStateInfos,
                ItemUnlockStateInfos = itemUnlockStateInfos,
                ChallengeCategoryUnlockStateInfos = challengeUnlockStateInfos, // Added for challenge unlock
            };
        }
        
        #endregion
    }
    
    public class GameUnlockStateJsonObject
    {
        [JsonProperty("craftRecipeUnlockStateInfos")] public List<CraftRecipeUnlockStateInfoJsonObject> CraftRecipeUnlockStateInfos;
        [JsonProperty("itemUnlockStateInfos")] public List<ItemUnlockStateInfoJsonObject> ItemUnlockStateInfos;
        [JsonProperty("challengeCategoryUnlockStateInfos")] public List<ChallengeUnlockStateInfoJsonObject> ChallengeCategoryUnlockStateInfos;
    }
}
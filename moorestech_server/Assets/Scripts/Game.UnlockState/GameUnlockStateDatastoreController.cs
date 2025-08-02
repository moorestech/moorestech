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
            // TODO 同じ処理がいろいろあるため共通化したい
            
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
            foreach (var challenge in MasterHolder.ChallengeMaster.ChallengeMasterElements)
            {
                var guid = challenge.ChallengeGuid;
                if (!ChallengeUnlockStateInfos.ContainsKey(guid))
                {
                    _challengeUnlockStateInfos.Add(guid, new ChallengeUnlockStateInfo(guid, challenge.InitialUnlocked));
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
        

        public IObservable<Guid> OnUnlockChallenge => _onUnlockChallenge;
        public IReadOnlyDictionary<Guid, ChallengeUnlockStateInfo> ChallengeUnlockStateInfos => _challengeUnlockStateInfos;

        private readonly Subject<Guid> _onUnlockChallenge = new();
        private readonly Dictionary<Guid, ChallengeUnlockStateInfo> _challengeUnlockStateInfos = new();
        public void UnlockChallenge(Guid challengeGuid)
        {
            if (!ChallengeUnlockStateInfos.ContainsKey(challengeGuid))
            {
                Debug.LogError($"[UnlockChallenge] Challenge not found: {challengeGuid}");
                return;
            }
            ChallengeUnlockStateInfos[challengeGuid].Unlock();
            _onUnlockChallenge.OnNext(challengeGuid);
        }
        
        
        #region SaveLoad
        
        public void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject)
        {
            LoadRecipeUnlockStateInfos();
            LoadItemUnlockStateInfos();
            LoadChallengeUnlockStateInfos();
            
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

            void LoadChallengeUnlockStateInfos()
            {
                foreach (var challengeUnlockStateInfo in stateJsonObject.ChallengeUnlockStateInfos)
                {
                    var state = new ChallengeUnlockStateInfo(challengeUnlockStateInfo);
                    _challengeUnlockStateInfos[state.ChallengeGuid] = state;
                }
            }
            
            #endregion
        }
        
        public GameUnlockStateJsonObject GetSaveJsonObject()
        {
            var recipeUnlockStateInfos = CraftRecipeUnlockStateInfos.Values.Select(r => new CraftRecipeUnlockStateInfoJsonObject(r)).ToList();
            var itemUnlockStateInfos = ItemUnlockStateInfos.Values.Select(i => new ItemUnlockStateInfoJsonObject(i)).ToList();
            var challengeUnlockStateInfos = ChallengeUnlockStateInfos.Values.Select(c => new ChallengeUnlockStateInfoJsonObject(c)).ToList(); // Added for challenge unlock
            return new GameUnlockStateJsonObject
            {
                CraftRecipeUnlockStateInfos = recipeUnlockStateInfos,
                ItemUnlockStateInfos = itemUnlockStateInfos,
                ChallengeUnlockStateInfos = challengeUnlockStateInfos, // Added for challenge unlock
            };
        }
        
        #endregion
    }
    
    public class GameUnlockStateJsonObject
    {
        [JsonProperty("craftRecipeUnlockStateInfos")] public List<CraftRecipeUnlockStateInfoJsonObject> CraftRecipeUnlockStateInfos;
        [JsonProperty("itemUnlockStateInfos")] public List<ItemUnlockStateInfoJsonObject> ItemUnlockStateInfos;
        [JsonProperty("challengeUnlockStateInfos")] public List<ChallengeUnlockStateInfoJsonObject> ChallengeUnlockStateInfos;
    }
}
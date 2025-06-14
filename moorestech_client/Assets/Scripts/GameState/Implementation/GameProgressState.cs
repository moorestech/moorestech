using System;
using System.Collections.Generic;
using Client.Network.API;
using Core.Master;
using Game.UnlockState;
using Game.UnlockState.States;
using MessagePack;
using Server.Event.EventReceive;

namespace GameState.Implementation
{
    public class GameProgressState : IGameProgressState, IVanillaApiConnectable
    {
        private readonly GameUnlockStateDataImpl _unlockStateData;
        private readonly ChallengeStateImpl _challengeState;
        private readonly CraftTreeStateImpl _craftTreeState;
        
        public IGameUnlockStateData Unlocks => _unlockStateData;
        public IReadOnlyChallengeState Challenges => _challengeState;
        public IReadOnlyCraftTreeState CraftTree => _craftTreeState;

        public GameProgressState()
        {
            _unlockStateData = new GameUnlockStateDataImpl();
            _challengeState = new ChallengeStateImpl();
            _craftTreeState = new CraftTreeStateImpl();
        }
        
        public void ConnectToVanillaApi(VanillaApi vanillaApi, InitialHandshakeResponse initialHandshakeResponse)
        {
            // Initialize unlock state from handshake response
            var unlockState = initialHandshakeResponse.UnlockState;
            UpdateUnlockState(
                unlockState.UnlockedCraftRecipeGuids,
                unlockState.LockedCraftRecipeGuids,
                unlockState.UnlockedItemIds,
                unlockState.LockedItemIds,
                unlockState.UnlockedChallengeGuids,
                unlockState.LockedChallengeGuids);
            
            // Initialize challenge state
            _challengeState.Initialize(initialHandshakeResponse.Challenge);
            
            // Initialize craft tree state
            _craftTreeState.Initialize(initialHandshakeResponse.CraftTree);
            
            // Subscribe to unlock events
            SubscribeToUnlockEvents(vanillaApi);
        }
        
        private void SubscribeToUnlockEvents(VanillaApi vanillaApi)
        {
            // Unlock event
            vanillaApi.Event.SubscribeEventResponse(UnlockedEventPacket.EventTag, payload =>
            {
                var data = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(payload);
                
                switch (data.UnlockEventType)
                {
                    case UnlockEventType.CraftRecipe:
                        _unlockStateData.UnlockCraftRecipe(data.UnlockedCraftRecipeGuid);
                        break;
                    case UnlockEventType.Item:
                        _unlockStateData.UnlockItem(data.UnlockedItemId);
                        break;
                    case UnlockEventType.Challenge:
                        _unlockStateData.UnlockChallenge(data.UnlockedChallengeGuid);
                        break;
                }
            });
        }

        private void UpdateUnlockState(
            List<Guid> unlockedCraftRecipeGuids, 
            List<Guid> lockedCraftRecipeGuids,
            List<ItemId> unlockedItemIds,
            List<ItemId> lockedItemIds,
            List<Guid> unlockedChallengeGuids,
            List<Guid> lockedChallengeGuids)
        {
            _unlockStateData.UpdateState(
                unlockedCraftRecipeGuids, lockedCraftRecipeGuids,
                unlockedItemIds, lockedItemIds,
                unlockedChallengeGuids, lockedChallengeGuids);
        }

        private class GameUnlockStateDataImpl : IGameUnlockStateData
        {
            private readonly Dictionary<Guid, CraftRecipeUnlockStateInfo> _recipeUnlockStateInfos = new();
            private readonly Dictionary<ItemId, ItemUnlockStateInfo> _itemUnlockStateInfos = new();
            private readonly Dictionary<Guid, ChallengeUnlockStateInfo> _challengeUnlockStateInfos = new();

            public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos => _recipeUnlockStateInfos;
            public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos => _itemUnlockStateInfos;
            public IReadOnlyDictionary<Guid, ChallengeUnlockStateInfo> ChallengeUnlockStateInfos => _challengeUnlockStateInfos;

            public void UpdateState(
                List<Guid> unlockedCraftRecipeGuids, 
                List<Guid> lockedCraftRecipeGuids,
                List<ItemId> unlockedItemIds,
                List<ItemId> lockedItemIds,
                List<Guid> unlockedChallengeGuids,
                List<Guid> lockedChallengeGuids)
            {
                // Update recipes
                foreach (var guid in lockedCraftRecipeGuids)
                {
                    _recipeUnlockStateInfos[guid] = new CraftRecipeUnlockStateInfo(guid, false);
                }
                foreach (var guid in unlockedCraftRecipeGuids)
                {
                    _recipeUnlockStateInfos[guid] = new CraftRecipeUnlockStateInfo(guid, true);
                }

                // Update items
                foreach (var itemId in lockedItemIds)
                {
                    _itemUnlockStateInfos[itemId] = new ItemUnlockStateInfo(itemId, false);
                }
                foreach (var itemId in unlockedItemIds)
                {
                    _itemUnlockStateInfos[itemId] = new ItemUnlockStateInfo(itemId, true);
                }

                // Update challenges
                foreach (var guid in lockedChallengeGuids)
                {
                    _challengeUnlockStateInfos[guid] = new ChallengeUnlockStateInfo(guid, false);
                }
                foreach (var guid in unlockedChallengeGuids)
                {
                    _challengeUnlockStateInfos[guid] = new ChallengeUnlockStateInfo(guid, true);
                }
            }
            
            public void UnlockCraftRecipe(Guid guid)
            {
                _recipeUnlockStateInfos[guid] = new CraftRecipeUnlockStateInfo(guid, true);
            }
            
            public void UnlockItem(ItemId itemId)
            {
                _itemUnlockStateInfos[itemId] = new ItemUnlockStateInfo(itemId, true);
            }
            
            public void UnlockChallenge(Guid guid)
            {
                _challengeUnlockStateInfos[guid] = new ChallengeUnlockStateInfo(guid, true);
            }
        }
    }

    public class ChallengeStateImpl : IReadOnlyChallengeState
    {
        private ChallengeResponse _challengeData;
        
        public ChallengeResponse ChallengeData => _challengeData;
        
        public void Initialize(ChallengeResponse challengeData)
        {
            _challengeData = challengeData;
        }
    }

    public class CraftTreeStateImpl : IReadOnlyCraftTreeState
    {
        private CraftTreeResponse _craftTreeData;
        
        public CraftTreeResponse CraftTreeData => _craftTreeData;
        
        public void Initialize(CraftTreeResponse craftTreeData)
        {
            _craftTreeData = craftTreeData;
        }
    }
}
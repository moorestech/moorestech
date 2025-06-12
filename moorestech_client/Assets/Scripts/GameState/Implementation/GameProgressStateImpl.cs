using System;
using System.Collections.Generic;
using Core.Master;
using Game.UnlockState;
using Game.UnlockState.States;

namespace GameState.Implementation
{
    public class GameProgressStateImpl : IGameProgressState
    {
        private readonly GameUnlockStateDataImpl _unlockStateData;

        public GameProgressStateImpl()
        {
            _unlockStateData = new GameUnlockStateDataImpl();
        }

        public IGameUnlockStateData Unlocks => _unlockStateData;

        public IReadOnlyChallengeState Challenges => new ChallengeStateImpl();

        public IReadOnlyCraftTreeState CraftTree => new CraftTreeStateImpl();

        public void UpdateUnlockState(
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
        }
    }

    public class ChallengeStateImpl : IReadOnlyChallengeState
    {
        // TODO: Implement based on existing challenge system
    }

    public class CraftTreeStateImpl : IReadOnlyCraftTreeState
    {
        // TODO: Implement based on existing craft tree system
    }
}
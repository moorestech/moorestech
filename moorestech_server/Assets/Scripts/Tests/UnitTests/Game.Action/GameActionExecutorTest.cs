using System;
using System.Collections.Generic;
using Game.Action;
using Game.UnlockState;
using Game.UnlockState.States;
using Mooresmaster.Model.ChallengeActionModule;
using NUnit.Framework;
using Core.Master;
using UniRx;

namespace Tests.UnitTests.Game.Action
{
    [TestFixture]
    public class GameActionExecutorTest
    {
        private IGameActionExecutor _gameActionExecutor;
        private MockGameUnlockStateDataController _mockGameUnlockStateDataController;

        [SetUp]
        public void Setup()
        {
            _mockGameUnlockStateDataController = new MockGameUnlockStateDataController();
            _gameActionExecutor = new GameActionExecutor(_mockGameUnlockStateDataController);
        }

        [Test]
        public void ExecuteAction_UnlockCraftRecipe_CallsUnlockCraftRecipeForEachGuid()
        {
            // Arrange
            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            var unlockParam = new UnlockCraftRecipeChallengeActionParam(
                new[] { guid1, guid2 }
            );
            var action = new ChallengeActionElement(
                ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe,
                unlockParam
            );

            // Act
            _gameActionExecutor.ExecuteAction(action);

            // Assert
            Assert.IsTrue(_mockGameUnlockStateDataController.UnlockedCraftRecipes.Contains(guid1));
            Assert.IsTrue(_mockGameUnlockStateDataController.UnlockedCraftRecipes.Contains(guid2));
            Assert.AreEqual(2, _mockGameUnlockStateDataController.UnlockedCraftRecipes.Count);
        }

        [Test]
        public void ExecuteAction_UnlockChallengeCategory_CallsUnlockChallengeForEachGuid()
        {
            // Arrange
            var challengeGuid1 = Guid.NewGuid();
            var challengeGuid2 = Guid.NewGuid();
            var unlockParam = new UnlockChallengeCategoryChallengeActionParam(
                new[] { challengeGuid1, challengeGuid2 }
            );
            var action = new ChallengeActionElement(
                ChallengeActionElement.ChallengeActionTypeConst.unlockChallengeCategory,
                unlockParam
            );

            // Act
            _gameActionExecutor.ExecuteAction(action);

            // Assert
            Assert.IsTrue(_mockGameUnlockStateDataController.UnlockedChallenges.Contains(challengeGuid1));
            Assert.IsTrue(_mockGameUnlockStateDataController.UnlockedChallenges.Contains(challengeGuid2));
            Assert.AreEqual(2, _mockGameUnlockStateDataController.UnlockedChallenges.Count);
        }

        [Test]
        public void ExecuteAction_NullAction_DoesNotThrow()
        {
            // Arrange
            ChallengeActionElement action = null;

            // Act & Assert
            Assert.DoesNotThrow(() => _gameActionExecutor.ExecuteAction(action));
        }

        [Test]
        public void ExecuteAction_UnknownActionType_DoesNotThrow()
        {
            // Arrange
            var action = new ChallengeActionElement(
                "unknown_action_type",
                null
            );

            // Act & Assert
            Assert.DoesNotThrow(() => _gameActionExecutor.ExecuteAction(action));
        }

        #region Mock Classes

        private class MockGameUnlockStateDataController : IGameUnlockStateDataController
        {
            public HashSet<Guid> UnlockedCraftRecipes { get; } = new HashSet<Guid>();
            public HashSet<ItemId> UnlockedItems { get; } = new HashSet<ItemId>();
            public HashSet<Guid> UnlockedChallenges { get; } = new HashSet<Guid>();

            private readonly Dictionary<Guid, CraftRecipeUnlockStateInfo> _craftRecipeUnlockStateInfos = new Dictionary<Guid, CraftRecipeUnlockStateInfo>();
            private readonly Dictionary<ItemId, ItemUnlockStateInfo> _itemUnlockStateInfos = new Dictionary<ItemId, ItemUnlockStateInfo>();
            private readonly Dictionary<Guid, ChallengeCategoryUnlockStateInfo> _challengeCategoryUnlockStateInfos = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();

            // IGameUnlockStateData properties
            public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos => _craftRecipeUnlockStateInfos;
            public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos => _itemUnlockStateInfos;
            public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos => _challengeCategoryUnlockStateInfos;

            // IGameUnlockStateDataController properties
            public IObservable<Guid> OnUnlockCraftRecipe => new Subject<Guid>();
            public IObservable<ItemId> OnUnlockItem => new Subject<ItemId>();
            public IObservable<Guid> OnUnlockChallengeCategory => new Subject<Guid>();

            public void UnlockCraftRecipe(Guid recipeGuid)
            {
                UnlockedCraftRecipes.Add(recipeGuid);
            }

            public void UnlockItem(ItemId itemId)
            {
                UnlockedItems.Add(itemId);
            }

            public void UnlockChallenge(Guid challengeGuid)
            {
                UnlockedChallenges.Add(challengeGuid);
            }

            public void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject)
            {
                // Not used in this test
            }

            public GameUnlockStateJsonObject GetSaveJsonObject()
            {
                return new GameUnlockStateJsonObject();
            }
        }

        #endregion
    }
}
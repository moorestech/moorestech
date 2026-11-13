using System;
using System.Linq;
using Client.Ui.State.Commands;
using Client.Ui.State.Recipe.Commands;
using Core.Master;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Recipe.Screen
{
    /// <summary>
    /// クラフトの操作が解放済みだけを送り、未知・未解放は送らずに断る（L0）
    /// The craft operation sends only unlocked recipes and refuses unknown or locked ones unsent (L0)
    /// </summary>
    public class RecipeCommandsTest
    {
        private IGameUnlockStateDataController _unlock;

        [SetUp]
        public void SetUp()
        {
            // テスト mod のマスタとサーバーの解放状態を作る
            // Build the test-mod master and the server unlock state
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _unlock = provider.GetService<IGameUnlockStateDataController>();
        }

        [Test]
        public void 解放済みのレシピは送って受理する()
        {
            var recipe = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes().First(r => _unlock.CraftRecipeUnlockStateInfos[r.CraftRecipeGuid].IsUnlocked);
            var sender = new RecordingCraftRequestSender();

            var result = new RecipeCommands(_unlock, sender).Craft(recipe.CraftRecipeGuid);

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            CollectionAssert.AreEqual(new[] { recipe.CraftRecipeGuid }, sender.Sent);
        }

        [Test]
        public void 未知のGUIDと未解放は送らずに良性でない拒否を返す()
        {
            // 拒否のたびに警告ログが出ることも確かめる
            // Also confirm each refusal logs a warning
            var locked = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes().First(r => !_unlock.CraftRecipeUnlockStateInfos[r.CraftRecipeGuid].IsUnlocked);
            var sender = new RecordingCraftRequestSender();
            var commands = new RecipeCommands(_unlock, sender);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[craft\.execute\] rejected: invalid_recipe"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[craft\.execute\] rejected: recipe_locked"));

            var unknown = (UiCommandRejected)commands.Craft(Guid.NewGuid());
            var lockedResult = (UiCommandRejected)commands.Craft(locked.CraftRecipeGuid);

            // 既存の機械レシピと共用の拒否理由・Web と同じ失敗コード・良性でない（D-39）
            // Existing reasons shared with machine recipes, Web-identical failure codes, non-benign (D-39)
            Assert.AreEqual(UiCommandRejectReason.RecipeInvalid, unknown.Reason);
            Assert.AreEqual("invalid_recipe", UiCommandRejectReasonTraits.ToWireCode(unknown.Reason));
            Assert.AreEqual(UiCommandRejectReason.RecipeLocked, lockedResult.Reason);
            Assert.AreEqual("recipe_locked", UiCommandRejectReasonTraits.ToWireCode(lockedResult.Reason));
            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(unknown.Reason));
            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(lockedResult.Reason));
            Assert.IsEmpty(sender.Sent);
        }
    }
}

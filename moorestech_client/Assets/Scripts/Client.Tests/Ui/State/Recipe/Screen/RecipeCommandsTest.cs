using System;
using System.Linq;
using Client.Tests.Ui.State.Toast;
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
            var toaster = new RecordingUiCommandResultToaster();

            new RecipeCommands(_unlock, sender, toaster).Craft(recipe.CraftRecipeGuid);

            Assert.AreEqual(1, toaster.Reports.Count);
            Assert.AreEqual(RecipeCommands.CommandName, toaster.Reports[0].CommandName);
            Assert.IsInstanceOf<UiCommandAccepted>(toaster.Reports[0].Result);
            CollectionAssert.AreEqual(new[] { recipe.CraftRecipeGuid }, sender.Sent);
        }

        [Test]
        public void 未知のGUIDと未解放は送らずに良性でない拒否をトーストへ渡す()
        {
            // 拒否のたびに警告ログが出ることも確かめる
            // Also confirm each refusal logs a warning
            var locked = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes().First(r => !_unlock.CraftRecipeUnlockStateInfos[r.CraftRecipeGuid].IsUnlocked);
            var sender = new RecordingCraftRequestSender();
            var toaster = new RecordingUiCommandResultToaster();
            var commands = new RecipeCommands(_unlock, sender, toaster);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[craft\.execute\] rejected: invalid_recipe"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[craft\.execute\] rejected: recipe_locked"));

            commands.Craft(Guid.NewGuid());
            commands.Craft(locked.CraftRecipeGuid);
            Assert.AreEqual(2, toaster.Reports.Count);
            Assert.AreEqual(RecipeCommands.CommandName, toaster.Reports[0].CommandName);
            var unknown = (UiCommandRejected)toaster.Reports[0].Result;
            var lockedResult = (UiCommandRejected)toaster.Reports[1].Result;

            // 既存の機械レシピと共用の拒否理由・Web と同じ失敗コード・良性でない（D-39）
            // Existing reasons shared with machine recipes, Web-identical failure codes, non-benign (D-39)
            Assert.AreEqual(UiCommandRejectReason.RecipeInvalid, unknown.Reason);
            Assert.AreEqual("invalid_recipe", UiCommandRejectReasonTable.Get(unknown.Reason).WireCode);
            Assert.AreEqual(UiCommandRejectReason.RecipeLocked, lockedResult.Reason);
            Assert.AreEqual("recipe_locked", UiCommandRejectReasonTable.Get(lockedResult.Reason).WireCode);
            Assert.IsFalse(UiCommandRejectReasonTable.Get(unknown.Reason).IsBenign);
            Assert.IsFalse(UiCommandRejectReasonTable.Get(lockedResult.Reason).IsBenign);
            Assert.IsEmpty(sender.Sent);
        }
    }
}

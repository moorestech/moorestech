using System;
using Client.Tests.Ui.Screens.Recipe;
using Client.Ui.State.Commands;
using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// craft.execute が payload を読み、実在・解放の検査と送信を IRecipeCommands へ委ねることのテスト（検査そのものは RecipeCommandsTest）
    /// Tests that craft.execute reads the payload and delegates existence/unlock checks and sending to IRecipeCommands (the checks themselves live in RecipeCommandsTest)
    /// </summary>
    public class CraftActionTest
    {
        // 不正な形式・非文字列の guid はコマンドを呼ばずに invalid_recipe
        // A malformed or non-string guid yields invalid_recipe without calling the command
        [TestCase("not-a-guid")]
        [TestCase(42)]
        public void 不正なrecipeGuidはコマンドを呼ばずにinvalid_recipeを返す(object rawGuid)
        {
            var commands = new RecordingRecipeCommands();
            var result = Execute(commands, new JValue(rawGuid));

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_recipe", result.Error);
            Assert.IsEmpty(commands.Crafted);
        }

        // 読めた guid はコマンドへ渡し、受理は成功になる
        // A parsed guid is handed to the command and acceptance becomes success
        [Test]
        public void 読めたrecipeGuidはコマンドへ渡して受理を成功で返す()
        {
            var guid = Guid.NewGuid();
            var commands = new RecordingRecipeCommands();
            var result = Execute(commands, new JValue(guid.ToString()));

            Assert.IsTrue(result.Ok);
            Assert.IsNull(result.Error);
            CollectionAssert.AreEqual(new[] { guid }, commands.Crafted);
        }

        // コマンドの拒否は State の失敗コードの綴りのまま返る
        // A command refusal comes back spelled as the State failure code
        [Test]
        public void コマンドの拒否はStateの失敗コードで返す()
        {
            var commands = new RecordingRecipeCommands();
            commands.SetResult(UiCommandRejected.Of(UiCommandRejectReason.RecipeLocked));
            var result = Execute(commands, new JValue(Guid.NewGuid().ToString()));

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("recipe_locked", result.Error);
        }

        private static ActionResult Execute(RecordingRecipeCommands commands, JToken recipeGuid)
        {
            var payload = new JObject { ["recipeGuid"] = recipeGuid };
            return new CraftExecuteActionHandler(commands).ExecuteAsync(payload).GetAwaiter().GetResult();
        }
    }
}

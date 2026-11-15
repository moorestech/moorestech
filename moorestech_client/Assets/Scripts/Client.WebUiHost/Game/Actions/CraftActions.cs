using System;
using Client.Ui.State.Commands;
using Client.Ui.State.Recipe.Commands;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// craft.execute: payload の recipeGuid を読み、検査・送信・拒否ログは State の IRecipeCommands へ委ねる
    /// craft.execute: read the payload recipeGuid and delegate checking, sending and refusal logging to the State IRecipeCommands
    /// </summary>
    public class CraftExecuteActionHandler : IActionHandler
    {
        public string ActionType => "craft.execute";

        private readonly IRecipeCommands _commands;

        public CraftExecuteActionHandler(IRecipeCommands commands)
        {
            _commands = commands;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // 非文字列値でも例外を出さないよう JValue の中身を直接パターンマッチで取り出す
            // Pattern-match the raw JValue content so non-string values never throw
            if (payload["recipeGuid"] is not JValue { Value: string guidText }) return UniTask.FromResult(InvalidRecipe());
            if (!Guid.TryParse(guidText, out var recipeGuid)) return UniTask.FromResult(InvalidRecipe());

            // 実在・解放の検査と送信は UITK と同じ型付きコマンド 1 本を通す
            // Existence/unlock checks and sending go through the one typed command shared with UITK
            return UniTask.FromResult(ActionResult.FromCommandResult(_commands.Craft(recipeGuid)));

            #region Internal

            ActionResult InvalidRecipe()
            {
                return ActionResult.Fail(UiCommandRejectReasonTraits.ToWireCode(UiCommandRejectReason.RecipeInvalid));
            }

            #endregion
        }
    }
}

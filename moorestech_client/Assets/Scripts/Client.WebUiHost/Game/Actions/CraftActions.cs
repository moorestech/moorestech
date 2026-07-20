using System;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// craft.execute: 指定レシピのワンクリッククラフトをサーバーへ送信する
    /// craft.execute: send a one-click craft request for the given recipe
    /// </summary>
    public class CraftExecuteActionHandler : IActionHandler
    {
        public string ActionType => "craft.execute";

        private readonly IGameUnlockStateData _unlockStateData;

        public CraftExecuteActionHandler(IGameUnlockStateData unlockStateData)
        {
            _unlockStateData = unlockStateData;
        }

        // recipeGuid のパース・実在・解放を判定する純関数。成功時のみ recipeGuid を返す
        // Pure function validating recipeGuid parse/existence/unlock; yields recipeGuid only on success
        public static ActionResult ResolveCraftRecipe(JToken recipeGuidToken, IGameUnlockStateData unlockStateData, out Guid recipeGuid)
        {
            recipeGuid = Guid.Empty;

            // 非文字列値でも例外を出さないよう JValue の中身を直接パターンマッチで取り出す
            // Pattern-match the raw JValue content so non-string values never throw
            if (recipeGuidToken is not JValue { Value: string guidText }) return ActionResult.Fail("invalid_recipe");
            if (!Guid.TryParse(guidText, out recipeGuid)) return ActionResult.Fail("invalid_recipe");

            // 存在しないレシピと未解放レシピをここで弾く（サーバーはunlock検証を行わない）
            // Reject unknown and locked recipes here; the server does not validate unlock state
            if (!unlockStateData.CraftRecipeUnlockStateInfos.TryGetValue(recipeGuid, out var unlockInfo)) return ActionResult.Fail("invalid_recipe");
            if (!unlockInfo.IsUnlocked) return ActionResult.Fail("recipe_locked");

            return ActionResult.Success();
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // recipeGuid の検証は純関数へ集約。失敗結果はそのまま返す
            // Recipe validation is centralized in the pure function; return its failure as-is
            var resolveResult = ResolveCraftRecipe(payload["recipeGuid"], _unlockStateData, out var recipeGuid);
            if (!resolveResult.Ok) return UniTask.FromResult(resolveResult);

            // 素材所持チェックはサーバー側で行われるためここでは送信のみ
            // Material checks happen server-side; just send the request here
            ClientContext.VanillaApi.SendOnly.Craft(recipeGuid);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}

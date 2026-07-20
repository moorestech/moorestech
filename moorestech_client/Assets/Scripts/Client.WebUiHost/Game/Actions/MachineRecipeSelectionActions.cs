using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using Newtonsoft.Json.Linq;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// machine_recipe.select: 開いている機械の選択レシピを設定・解除する
    /// machine_recipe.select: sets or clears the selected recipe of the open machine
    /// </summary>
    public class MachineRecipeSelectActionHandler : IActionHandler
    {
        public string ActionType => "machine_recipe.select";

        private readonly SubInventoryState _subInventoryState;
        private readonly IGameUnlockStateData _unlockStateData;

        public MachineRecipeSelectActionHandler(SubInventoryState subInventoryState, IGameUnlockStateData unlockStateData)
        {
            _subInventoryState = subInventoryState;
            _unlockStateData = unlockStateData;
        }

        // recipeGuid のパース・実在・解放を判定する純関数
        // Pure function validating recipeGuid parsing, existence, and unlock state
        public static ActionResult ResolveSelectableRecipe(JToken recipeGuidToken, IGameUnlockStateData unlockStateData, out Guid recipeGuid)
        {
            recipeGuid = Guid.Empty;

            // 非文字列値と不正な GUID を同じ契約エラーへ集約する
            // Map non-string values and malformed GUIDs to the same contract error
            if (recipeGuidToken is not JValue { Value: string guidText }) return ActionResult.Fail("invalid_recipe");
            if (!Guid.TryParse(guidText, out recipeGuid)) return ActionResult.Fail("invalid_recipe");

            // クライアントの機械レシピ解放状態で存在と選択可否を判定する
            // Validate existence and selectability against client machine-recipe unlock state
            if (!unlockStateData.MachineRecipeUnlockStateInfos.TryGetValue(recipeGuid, out var unlockInfo)) return ActionResult.Fail("invalid_recipe");
            if (!unlockInfo.IsUnlocked) return ActionResult.Fail("recipe_locked");

            return ActionResult.Success();
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (payload["operation"] is not JValue { Value: string operation }) return ActionResult.Fail("invalid_operation");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return ActionResult.Fail("block_not_open");

            // 操作ごとの要求生成を検証と一体化し、成功した要求だけを送信する
            // Build each operation's request with validation and send only successful results
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            var resolveResult = ResolveRequest(out var request);
            if (!resolveResult.Ok) return resolveResult;

            var response = await ClientContext.VanillaApi.Response.SendMachineRecipeSelectionRequest(request, CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail("machine_recipe_request_failed");
            return ActionResult.Success();

            #region Internal

            ActionResult ResolveRequest(out MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest request)
            {
                request = null;
                if (operation == "set")
                {
                    var recipeResult = ResolveSelectableRecipe(payload["recipeGuid"], _unlockStateData, out var recipeGuid);
                    if (!recipeResult.Ok) return recipeResult;
                    request = MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(source.BlockPosition, recipeGuid, playerId);
                    return ActionResult.Success();
                }
                if (operation != "clear") return ActionResult.Fail("invalid_operation");

                request = MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateClearRequest(source.BlockPosition, playerId);
                return ActionResult.Success();
            }

            #endregion
        }
    }
}

using System;
using Client.Ui.State.Commands;
using Game.UnlockState;
using UnityEngine;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// クラフトの操作。CraftRecipeUnlockCheck を通ればサーバーへ送る
    /// Craft operation; sends to the server when CraftRecipeUnlockCheck passes
    /// </summary>
    public sealed class RecipeCommands : IRecipeCommands
    {
        public const string CommandName = "craft.execute";

        private readonly IGameUnlockStateData _unlockState;
        private readonly ICraftRequestSender _sender;

        public RecipeCommands(IGameUnlockStateData unlockState, ICraftRequestSender sender)
        {
            _unlockState = unlockState;
            _sender = sender;
        }

        public UiCommandResult Craft(Guid recipeGuid)
        {
            // 未知・未解放の GUID は送らずに断る
            // Refuse unknown or locked GUIDs without sending
            var rejection = CraftRecipeUnlockCheck.Check(_unlockState, recipeGuid);
            if (rejection.HasValue) return Reject(rejection.Value);

            _sender.Send(recipeGuid);
            return UiCommandAccepted.Instance;

            #region Internal

            UiCommandResult Reject(UiCommandRejectReason reason)
            {
                Debug.LogWarning($"[{CommandName}] rejected: {UiCommandRejectReasonTable.Get(reason).WireCode} recipe={recipeGuid}");
                return UiCommandRejected.Of(reason);
            }

            #endregion
        }
    }
}

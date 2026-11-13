using System;
using Client.Ui.State.Commands;
using Game.UnlockState;
using UnityEngine;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// クラフトの操作（Web CraftExecuteActionHandler の検証の写し）。通ればサーバーへ送る
    /// Craft operation (port of the Web CraftExecuteActionHandler checks); sends to the server when valid
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
            if (!_unlockState.CraftRecipeUnlockStateInfos.TryGetValue(recipeGuid, out var info)) return Reject(UiCommandRejectReason.RecipeInvalid);
            if (!info.IsUnlocked) return Reject(UiCommandRejectReason.RecipeLocked);

            _sender.Send(recipeGuid);
            return UiCommandAccepted.Instance;

            #region Internal

            UiCommandResult Reject(UiCommandRejectReason reason)
            {
                Debug.LogWarning($"[{CommandName}] rejected: {UiCommandRejectReasonTraits.ToWireCode(reason)} recipe={recipeGuid}");
                return new UiCommandRejected(reason, "");
            }

            #endregion
        }
    }
}

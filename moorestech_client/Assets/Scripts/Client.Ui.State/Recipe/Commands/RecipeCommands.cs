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
            // 解放表に無い GUID・未解放は送らずに断る（拒否理由は機械レシピと共用の既存値。D-40）
            // Refuse unknown or locked GUIDs unsent; the reasons are the existing values shared with machine recipes (D-39)
            if (!_unlockState.CraftRecipeUnlockStateInfos.TryGetValue(recipeGuid, out var info)) return Reject(UiCommandRejectReason.MachineRecipeInvalid);
            if (!info.IsUnlocked) return Reject(UiCommandRejectReason.MachineRecipeLocked);

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

using System;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Game.UnlockState;
using UnityEngine;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// クラフトの操作。解放表にあり解放済みならサーバーへ送り、結果はトーストへ渡す
    /// Craft operation; sends to the server when the recipe is in the unlock table and unlocked, and hands the result to the toaster
    /// </summary>
    public sealed class RecipeCommands : IRecipeCommands
    {
        public const string CommandName = "craft.execute";

        private readonly IGameUnlockStateData _unlockState;
        private readonly ICraftRequestSender _sender;
        private readonly IUiCommandResultToaster _toaster;

        public RecipeCommands(IGameUnlockStateData unlockState, ICraftRequestSender sender, IUiCommandResultToaster toaster)
        {
            _unlockState = unlockState;
            _sender = sender;
            _toaster = toaster;
        }

        public void Craft(Guid recipeGuid)
        {
            // 未知・未解放の GUID は送らずに断る
            // Refuse unknown or locked GUIDs without sending
            var rejection = UnlockRejection();
            if (rejection.HasValue)
            {
                Reject(rejection.Value);
                return;
            }

            _sender.Send(recipeGuid);
            _toaster.Report(CommandName, UiCommandAccepted.Instance);

            #region Internal

            UiCommandRejectReason? UnlockRejection()
            {
                // 解放表に無い→RecipeInvalid、未解放→RecipeLocked
                // Absent from the unlock table → RecipeInvalid, locked → RecipeLocked
                if (!_unlockState.CraftRecipeUnlockStateInfos.TryGetValue(recipeGuid, out var info)) return UiCommandRejectReason.RecipeInvalid;
                if (!info.IsUnlocked) return UiCommandRejectReason.RecipeLocked;
                return null;
            }

            void Reject(UiCommandRejectReason reason)
            {
                Debug.LogWarning($"[{CommandName}] rejected: {UiCommandRejectReasonTable.Get(reason).WireCode} recipe={recipeGuid}");
                _toaster.Report(CommandName, UiCommandRejected.Of(reason));
            }

            #endregion
        }
    }
}

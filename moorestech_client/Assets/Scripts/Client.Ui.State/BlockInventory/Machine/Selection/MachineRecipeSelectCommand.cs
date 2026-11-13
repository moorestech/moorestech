using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 開いた機械のレシピを選んで送る
    /// Selects and sends the open machine's recipe; checks and responses match the Web host's MachineRecipeSelectActionHandler
    /// </summary>
    public sealed class MachineRecipeSelectCommand : IMachineRecipeSelectCommand
    {
        public const string CommandName = "machine_recipe.select";

        private readonly IBlockInventoryPanelSource _panelSource;
        private readonly IMachineSectionModel _machine;
        private readonly IGameUnlockStateData _unlockState;

        public MachineRecipeSelectCommand(IBlockInventoryPanelSource panelSource, IMachineSectionModel machine, IGameUnlockStateData unlockState)
        {
            _panelSource = panelSource;
            _machine = machine;
            _unlockState = unlockState;
        }

        public async UniTask<UiCommandResult> Select(Guid recipeGuid)
        {
            // 送れない要求は非良性の拒否で返す（Web と同じくトーストする。GUID はここでログへ出す。D-38）
            // Requests that cannot go return a non-benign rejection (toasts like Web; the GUID is logged here; D-38)
            if (_panelSource.Current.Value is not OpenBlockInventoryPanel panel || !panel.IsOpenWithoutError) return Reject(UiCommandRejectReason.BlockNotOpen, "");
            if (!_unlockState.MachineRecipeUnlockStateInfos.TryGetValue(recipeGuid, out var unlock)) return Reject(UiCommandRejectReason.RecipeInvalid, "");
            if (!unlock.IsUnlocked) return Reject(UiCommandRejectReason.RecipeLocked, "");

            // 選択中と同じレシピは送らず受理
            // The already-selected recipe is accepted unsent, as Web onSelect; the mode closes on the request match
            if (_machine.Current.Value.TryGetPresent(out var machine) && machine.Detail.Selection.Matches(recipeGuid)) return UiCommandAccepted.Instance;

            // 応答はネットワーク由来なので null も拒否として扱う（Web ホストと同じ判定。例外は捕まえない）
            // The response comes from the network, so null also counts as a rejection (same check as the Web host; no exception is caught)
            var request = MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(panel.Position, recipeGuid);
            var response = await ClientContext.VanillaApi.Response.Block.SendMachineRecipeSelectionRequest(request, CancellationToken.None);
            if (response == null || !response.Success) return Reject(UiCommandRejectReason.MachineRecipeRequestFailed, response == null ? "no_response" : response.FailureReason.ToString());
            return UiCommandAccepted.Instance;

            #region Internal

            UiCommandResult Reject(UiCommandRejectReason reason, string serverReason)
            {
                Debug.LogWarning($"[{CommandName}] rejected: {UiCommandRejectReasonTraits.ToWireCode(reason)} recipe={recipeGuid} server={serverReason}");
                return UiCommandRejected.Of(reason);
            }

            #endregion
        }
    }
}

using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Ui.State.Commands;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;
using Game.Research;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Ui.State.Research.Commands
{
    /// <summary>
    /// 研究ツリー操作の本番実装（A-262）
    /// Production research tree operations (A-262)
    /// </summary>
    public sealed class ResearchTreeCommands : IResearchTreeCommands
    {
        public const string CompleteResearchCommandName = "research.complete";

        private readonly ResearchSelectionStore _selection;
        private readonly ResearchNodeStateStore _store;
        private readonly IUiCommandResultToaster _toaster;

        public ResearchTreeCommands(ResearchSelectionStore selection, ResearchNodeStateStore store, IUiCommandResultToaster toaster)
        {
            _selection = selection;
            _store = store;
            _toaster = toaster;
        }

        public void ToggleNodeSelection(Guid researchGuid)
        {
            _selection.Toggle(researchGuid);
        }

        public void CloseDetail()
        {
            _selection.Clear();
        }

        // 戻り値は無い。サーバーの結果は届いてからトーストへ渡す
        // No return value; the server result is reported to the toaster when it arrives
        public void CompleteResearch(Guid researchGuid)
        {
            CompleteAsync().Forget();

            #region Internal

            async UniTaskVoid CompleteAsync()
            {
                var response = await ClientContext.VanillaApi.Response.Progression.CompleteResearch(researchGuid, CancellationToken.None);
                _toaster.Report(CompleteResearchCommandName, ToCommandResult(response, _store, researchGuid));
            }

            #endregion
        }

        // 応答を状態へ反映し結果へ写す
        // Apply the response to state and map to a result
        internal static UiCommandResult ToCommandResult(CompleteResearchProtocol.ResponseCompleteResearchMessagePack response, ResearchNodeStateStore store, Guid researchGuid)
        {
            // 応答なしは失敗として扱う
            // No response is a failure
            if (response == null)
            {
                Debug.LogWarning($"[ResearchTreeCommands] CompleteResearch {researchGuid} returned no response");
                return UiCommandRejected.Of(UiCommandRejectReason.ResearchFailed);
            }

            // 応答の全状態を反映
            // Apply all response states
            var states = response.NodeState.ToDictionary();
            store.ApplyNodeStates(states);
            if (response.Success) return UiCommandAccepted.Instance;

            // 応答待ちの二度押しは 2 回目だけ失敗するが研究は済んでいるので受理にする
            // A double press fails only the second request, yet the research is done, so treat it as accepted
            if (states.TryGetValue(researchGuid, out var state) && state == ResearchNodeState.Completed)
            {
                Debug.LogWarning($"[ResearchTreeCommands] CompleteResearch {researchGuid} already completed");
                return UiCommandAccepted.Instance;
            }
            return UiCommandRejected.Of(UiCommandRejectReason.ResearchFailed);
        }
    }
}

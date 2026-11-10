using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Ui.State.Commands;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Ui.State.Research.Commands
{
    /// <summary>
    /// 研究ツリー操作の本番実装。研究結果のトーストはこのクラスの 1 か所で出す
    /// Production research tree operations; the research result toast is reported only here
    /// </summary>
    public sealed class ResearchTreeCommands : IResearchTreeCommands
    {
        private const string CompleteResearchCommandName = "research.complete";

        private readonly ResearchSelectionStore _selection;
        private readonly ResearchNodeStateStore _store;
        private readonly UiCommandResultToaster _toaster;

        public ResearchTreeCommands(ResearchSelectionStore selection, ResearchNodeStateStore store, UiCommandResultToaster toaster)
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

        public async UniTask CompleteResearch(Guid researchGuid)
        {
            var response = await ClientContext.VanillaApi.Response.Progression.CompleteResearch(researchGuid, CancellationToken.None);
            ReportResponse(researchGuid, response);
        }

        // 応答の結果をトーストへ出す（送信境界の外の L0 が通る口）
        // Reports the response result (the seam L0 tests drive past the send boundary)
        internal void ReportResponse(Guid researchGuid, CompleteResearchProtocol.ResponseCompleteResearchMessagePack response)
        {
            _toaster.Report(CompleteResearchCommandName, ToCommandResult(response, _store, researchGuid));
        }

        // 応答を状態へ反映し結果へ写す
        // Applies the response states and maps it to a result
        internal static UiCommandResult ToCommandResult(CompleteResearchProtocol.ResponseCompleteResearchMessagePack response, ResearchNodeStateStore store, Guid researchGuid)
        {
            // 応答なしは失敗として扱う
            // No response is a failure
            if (response == null)
            {
                Debug.LogWarning($"[ResearchTreeCommands] CompleteResearch {researchGuid} returned no response");
                return new UiCommandRejected(UiCommandRejectReason.ResearchFailed, "");
            }

            // 応答の全状態を反映
            // Apply all response states
            store.ApplyNodeStates(response.NodeState.ToDictionary());
            return response.Success ? UiCommandAccepted.Instance : new UiCommandRejected(UiCommandRejectReason.ResearchFailed, "");
        }
    }
}

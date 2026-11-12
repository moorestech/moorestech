using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Ui.State.Commands;
using Client.Ui.State.Research.Tree;
using Cysharp.Threading.Tasks;
using Game.Research;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Ui.State.Research.Commands
{
    /// <summary>
    /// 研究ツリー操作の本番実装。研究の結果は返すだけで、トーストは呼び手の View が出す（A-262）
    /// Production research tree operations; the research result is only returned and the calling view toasts it (A-262)
    /// </summary>
    public sealed class ResearchTreeCommands : IResearchTreeCommands
    {
        public const string CompleteResearchCommandName = "research.complete";

        private readonly ResearchSelectionStore _selection;
        private readonly ResearchNodeStateStore _store;

        public ResearchTreeCommands(ResearchSelectionStore selection, ResearchNodeStateStore store)
        {
            _selection = selection;
            _store = store;
        }

        public void ToggleNodeSelection(Guid researchGuid)
        {
            _selection.Toggle(researchGuid);
        }

        public void CloseDetail()
        {
            _selection.Clear();
        }

        public async UniTask<UiCommandResult> CompleteResearch(Guid researchGuid)
        {
            var response = await ClientContext.VanillaApi.Response.Progression.CompleteResearch(researchGuid, CancellationToken.None);
            return ToCommandResult(response, _store, researchGuid);
        }

        // 応答を状態へ反映し結果へ写す（送信境界の外の L0 が通る口）
        // Applies the response states and maps it to a result (the seam L0 tests drive past the send boundary)
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

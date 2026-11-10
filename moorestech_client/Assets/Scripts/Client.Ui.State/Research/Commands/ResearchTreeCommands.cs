using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Ui.State.Commands;
using Client.Ui.State.Research.Tree;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Ui.State.Research.Commands
{
    /// <summary>
    /// 研究ツリー操作の本番実装
    /// Production research tree operations
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
            // 応答なしは失敗として扱う
            // No response is a failure
            var response = await ClientContext.VanillaApi.Response.Progression.CompleteResearch(researchGuid, CancellationToken.None);
            if (response == null)
            {
                Debug.LogWarning($"[ResearchTreeCommands] CompleteResearch {researchGuid} returned no response");
                return new UiCommandRejected(UiCommandRejectReason.ResearchFailed, "");
            }

            // 応答の全状態を反映
            // Apply all response states
            _store.ApplyNodeStates(response.NodeState.ToDictionary());
            return response.Success ? UiCommandAccepted.Instance : new UiCommandRejected(UiCommandRejectReason.ResearchFailed, "");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Research;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// research.tree トピック: 研究マスタ + サーバー状態を合成して push（ResearchTree 突入時に再取得）
    /// research.tree topic: merges research master with server states and pushes (refetch on entering ResearchTree)
    /// </summary>
    public class ResearchTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "research.tree";

        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private Dictionary<Guid, ResearchNodeState> _nodeStates = new();
        private CancellationTokenSource _cts;
        private bool _disposed;

        public ResearchTopic(WebSocketHub hub, UIStateControl uiStateControl)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;

            // state遷移を購読し、ResearchTree 突入で状態を取り直す
            // Subscribe to state transitions and refetch states on entering ResearchTree
            _uiStateControl.OnStateChanged += OnStateChanged;
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _uiStateControl.OnStateChanged -= OnStateChanged;
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // research.complete 応答の最新全ノード状態を反映する公開口
        // Public entry to apply the latest node states from a research.complete response
        public void ApplyNodeStates(Dictionary<Guid, ResearchNodeState> nodeStates)
        {
            _nodeStates = nodeStates;
            _hub.Publish(TopicName, BuildJson());
        }

        private void OnStateChanged(UIStateEnum state)
        {
            // ResearchTree 突入時のみサーバーから最新状態を取り直す（uGUI と同じ駆動）
            // Refetch server states only on entering ResearchTree (same trigger as uGUI)
            if (state != UIStateEnum.ResearchTree) return;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            RefreshAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid RefreshAsync(CancellationToken ct)
        {
            var states = await ClientContext.VanillaApi.Response.GetResearchNodeStates(ct);
            if (_disposed || ct.IsCancellationRequested || states == null) return;
            ApplyNodeStates(states);
        }

        private string BuildJson()
        {
            var dto = new ResearchTreeDto { Nodes = new List<ResearchNodeDto>() };
            foreach (var master in MasterHolder.ResearchMaster.GetAllResearches())
            {
                dto.Nodes.Add(ResearchNodeDtoFactory.Create(master, _nodeStates));
            }
            return WebUiJson.Serialize(dto);
        }
    }
}

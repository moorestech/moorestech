using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using Game.Research;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Research
{
    /// <summary>
    /// 研究ノードの状態（サーバーが正本）を持ち、研究画面への突入と表示中の所持変化で取り直す
    /// Holds the research node states (owned by the server) and refetches them on entering the research screen and on inventory moves while it is shown
    /// </summary>
    public sealed class ResearchNodeStateStore : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly LocalPlayerInventoryController _inventoryController;
        private readonly Subject<Unit> _onNodeStatesChanged = new();
        private CancellationTokenSource _cts;
        private bool _researchScreenActive;

        public IReadOnlyDictionary<Guid, ResearchNodeState> NodeStates { get; private set; } = new Dictionary<Guid, ResearchNodeState>();
        public IObservable<Unit> OnNodeStatesChanged => _onNodeStatesChanged;

        public ResearchNodeStateStore(UIStateControl uiStateControl, LocalPlayerInventoryController inventoryController)
        {
            _uiStateControl = uiStateControl;
            _inventoryController = inventoryController;
        }

        public void Initialize()
        {
            // state遷移を購読し、ResearchTree 突入で状態を取り直す
            // Subscribe to state transitions and refetch states on entering ResearchTree
            _uiStateControl.OnStateChanged.Subscribe(OnStateChanged);

            // 充足の正本はサーバーstateなので、所持が動いたら研究画面表示中は取り直す
            // Server state owns sufficiency, so refetch on inventory moves while the research screen is up
            _inventoryController.LocalPlayerInventory.OnItemChange.Subscribe(_ => RefreshWhileResearchScreenActive());
            _inventoryController.OnInventoryRefreshed.Subscribe(_ => RefreshWhileResearchScreenActive());
        }

        // research.complete 応答の最新全ノード状態を反映する
        // Apply the latest node states from a research.complete response
        public void ApplyNodeStates(Dictionary<Guid, ResearchNodeState> nodeStates)
        {
            NodeStates = nodeStates;
            _onNodeStatesChanged.OnNext(Unit.Default);
        }

        private void OnStateChanged(UIStateEnum state)
        {
            // ResearchTree 突入時のみサーバーから最新状態を取り直す（uGUI と同じ駆動）
            // Refetch server states only on entering ResearchTree (same trigger as uGUI)
            _researchScreenActive = state == UIStateEnum.ResearchTree;
            if (!_researchScreenActive) return; // silent-ok: 閉画面では取得不要 / 次回突入で最新状態を取得する
            StartRefresh();
        }

        // 研究画面を見ていない間の所持変化は、次の突入時の取り直しで足りる
        // Inventory moves while the screen is closed are covered by the refetch on the next entry
        private void RefreshWhileResearchScreenActive()
        {
            if (!_researchScreenActive) return; // silent-ok: 閉画面では取得不要 / 次回突入で最新状態を取得する
            StartRefresh();
        }

        private void StartRefresh()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            RefreshAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid RefreshAsync(CancellationToken ct)
        {
            var states = await ClientContext.VanillaApi.Response.Progression.GetResearchNodeStates(ct);
            if (ct.IsCancellationRequested) return; // silent-ok: 新しい取得に交代済み / 後続の取得が状態を通知する

            // 応答なしは前回の状態を保ち、次の突入・所持変化で取り直す
            // On no response keep the previous states; the next entry or inventory move refetches
            if (states == null)
            {
                Debug.LogWarning("[ResearchNodeStateStore] GetResearchNodeStates returned no response; keeping the previous node states");
                return;
            }
            ApplyNodeStates(states);
        }
    }
}

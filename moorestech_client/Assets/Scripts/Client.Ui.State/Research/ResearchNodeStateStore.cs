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

namespace Client.Ui.State.Research
{
    /// <summary>
    /// 研究ノード状態を持ち取り直す
    /// Holds research node states and refetches them
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
            // ResearchTree突入で取り直し
            // Refetch on entering ResearchTree
            _uiStateControl.OnStateChanged.Subscribe(OnStateChanged);

            // 充足の正本はサーバーstateなので、所持が動いたら研究画面表示中は取り直す
            // Server state owns sufficiency, so refetch on inventory moves while the research screen is up
            _inventoryController.LocalPlayerInventory.OnItemChange.Subscribe(_ => RefreshWhileResearchScreenActive());
            _inventoryController.OnInventoryRefreshed.Subscribe(_ => RefreshWhileResearchScreenActive());

            #region Internal

            void OnStateChanged(UIStateEnum state)
            {
                // 突入時のみ取り直す（uGUIと同じ）
                // Refetch only on entry (same as uGUI)
                _researchScreenActive = state == UIStateEnum.ResearchTree;
                if (!_researchScreenActive) return; // silent-ok: 閉画面では取得不要 / 次回突入で最新状態を取得する
                StartRefresh();
            }

            #endregion
        }

        // complete応答の全状態を反映
        // Apply all states from the complete response
        public void ApplyNodeStates(Dictionary<Guid, ResearchNodeState> nodeStates)
        {
            NodeStates = nodeStates;
            _onNodeStatesChanged.OnNext(Unit.Default);
        }

        // 閉画面の所持変化は無視
        // Ignore inventory moves while closed
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

            #region Internal

            async UniTaskVoid RefreshAsync(CancellationToken ct)
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

            #endregion
        }
    }
}

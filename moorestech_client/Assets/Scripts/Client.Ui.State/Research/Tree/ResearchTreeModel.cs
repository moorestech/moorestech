using Client.Localization;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Routing;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 研究ツリー表示の組み直しと配信
    /// Rebuilds and publishes the research tree display
    /// </summary>
    public sealed class ResearchTreeModel : IInitializable
    {
        private readonly ResearchNodeCatalog _catalog;
        private readonly ResearchNodeStateStore _store;
        private readonly ResearchSelectionStore _selection;
        private readonly IPlayerInventoryOperations _inventoryOperations;
        private readonly UiRoutingState _routing;
        private readonly ReactiveProperty<ResearchTreeViewModel> _current = new(ResearchTreeViewModel.Empty);
        private bool _screenShown;

        public IReadOnlyReactiveProperty<ResearchTreeViewModel> Current => _current;

        public ResearchTreeModel(ResearchNodeCatalog catalog, ResearchNodeStateStore store, ResearchSelectionStore selection, IPlayerInventoryOperations inventoryOperations, UiRoutingState routing)
        {
            _catalog = catalog;
            _store = store;
            _selection = selection;
            _inventoryOperations = inventoryOperations;
            _routing = routing;
        }

        public void Initialize()
        {
            // 状態・選択・所持・言語の変化は研究画面の表示中だけ組み直す（前例 ResearchNodeStateStore.RefreshWhileResearchScreenActive）
            // State, selection, inventory and language changes rebuild only while the research screen is shown (precedent ResearchNodeStateStore.RefreshWhileResearchScreenActive)
            _store.OnNodeStatesChanged.Subscribe(_ => OnNodeStatesReceived());
            _inventoryOperations.OnSnapshotChanged.Subscribe(_ => RebuildWhileShown());
            Localize.OnLanguageChanged.Subscribe(_ => RebuildWhileShown());
            _selection.Current.Skip(1).Subscribe(_ => RebuildWhileShown());

            // 表示の突入で1回組む
            // Build once on entering the screen
            _routing.Current.Subscribe(snapshot =>
            {
                _screenShown = snapshot.Screen == UiScreen.ResearchTree;
                RebuildWhileShown();
            });

            #region Internal

            void OnNodeStatesReceived()
            {
                WarnMissingNodeStates();
                RebuildWhileShown();
            }

            // 受けた状態に欠けたノードは Web の既定で描くので、受信ごとに件数を出して気づけるようにする（D-29）
            // Missing nodes render with the Web default, so log their count on every receipt to make the gap visible (D-29)
            void WarnMissingNodeStates()
            {
                var states = _store.NodeStates;
                var missing = 0;
                foreach (var node in _catalog.Nodes)
                {
                    if (!states.ContainsKey(node.Guid)) missing++;
                }
                if (missing == 0) return;
                Debug.LogWarning($"[ResearchTreeModel] {missing} research node(s) have no server state; drawn as UnresearchableAllReasons like the Web bridge");
            }

            #endregion
        }

        private void RebuildWhileShown()
        {
            if (!_screenShown) return; // silent-ok: 閉画面では組まない / 次の表示の突入で最新を組み、録画 uitk-research-tree の開き直しの段が見る
            Rebuild();
        }

        private void Rebuild()
        {
            // 所持数はメインの枠だけで数える（Web の buildOwnedCounts(inventory.mainSlots)。読み口は持ち物の操作と同じ）
            // Owned counts come from the main slots only (the Web buildOwnedCounts(inventory.mainSlots); same read port as inventory operations)
            var owned = OwnedItemCounts.Build(_inventoryOperations.ReadSnapshot().MainSlots);
            _current.Value = ResearchTreeViewModelFactory.Create(_catalog.Nodes, _store.NodeStates, owned, _selection.Current.Value);
        }
    }
}

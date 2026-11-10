using Client.Game.InGame.UI.Inventory.Main;
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
    /// 研究ツリーの表示を源から組み、研究画面の表示中だけ源が動くたびに同期で組み直して配る（A-57）。表示の出し入れは Presenter と View が持つ
    /// Builds the research tree display from its sources and rebuilds it synchronously on every change while the research screen is shown (A-57); showing is owned by the Presenter and View
    /// </summary>
    public sealed class ResearchTreeModel : IInitializable
    {
        private readonly ResearchNodeCatalog _catalog;
        private readonly ResearchNodeStateStore _store;
        private readonly ResearchSelectionStore _selection;
        private readonly LocalPlayerInventoryController _inventory;
        private readonly IPlayerInventoryOperations _inventoryOperations;
        private readonly UiRoutingState _routing;
        private readonly ReactiveProperty<ResearchTreeViewModel> _current = new(ResearchTreeViewModel.Empty);
        private bool _screenShown;

        public IReadOnlyReactiveProperty<ResearchTreeViewModel> Current => _current;

        public ResearchTreeModel(ResearchNodeCatalog catalog, ResearchNodeStateStore store, ResearchSelectionStore selection, LocalPlayerInventoryController inventory, IPlayerInventoryOperations inventoryOperations, UiRoutingState routing)
        {
            _catalog = catalog;
            _store = store;
            _selection = selection;
            _inventory = inventory;
            _inventoryOperations = inventoryOperations;
            _routing = routing;
        }

        public void Initialize()
        {
            // 状態・選択・所持・言語の変化は研究画面の表示中だけ組み直す（前例 ResearchNodeStateStore.RefreshWhileResearchScreenActive）
            // State, selection, inventory and language changes rebuild only while the research screen is shown (precedent ResearchNodeStateStore.RefreshWhileResearchScreenActive)
            _store.OnNodeStatesChanged.Subscribe(_ => OnNodeStatesReceived());
            _inventory.LocalPlayerInventory.OnItemChange.Subscribe(_ => RebuildWhileShown());
            _inventory.OnInventoryRefreshed.Subscribe(_ => RebuildWhileShown());
            Localize.OnLanguageChanged.Subscribe(_ => RebuildWhileShown());
            _selection.Current.Skip(1).Subscribe(_ => RebuildWhileShown());

            // 表示の突入で 1 回組む（閉じている間に捨てた変化をここで取り込む。購読の初回が今の画面を流す）
            // Build once on entering the screen (picks up changes dropped while closed; the first notification carries the current screen)
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

        // 受けた状態に欠けたノードは Web の既定で描くので、受信ごとに件数を出して気づけるようにする（D-29）
        // Missing nodes render with the Web default, so log their count on every receipt to make the gap visible (D-29)
        private void WarnMissingNodeStates()
        {
            var states = _store.NodeStates;
            if (states.Count == 0) return; // silent-ok: 取り直しの前は全ノードが未着で正常 / 応答が入った時点でこの数え直しが走る
            var missing = 0;
            foreach (var node in _catalog.Nodes)
            {
                if (!states.ContainsKey(node.Guid)) missing++;
            }
            if (missing == 0) return;
            Debug.LogWarning($"[ResearchTreeModel] {missing} research node(s) have no server state; drawn as UnresearchableAllReasons like the Web bridge");
        }
    }
}

using System.Collections.Generic;
using Client.Localization;
using Client.Network.API;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Catalog;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Source;
using Client.Ui.State.Routing;
using Core.Master;
using Game.UnlockState;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// レシピ画面の源を直に購読し、表示中だけ一覧とビューアの VM を同期に組み直す（A-57）
    /// Subscribes to the recipe screen sources directly and rebuilds the list and viewer VMs synchronously while shown (A-57)
    /// </summary>
    public sealed class RecipeScreenModel : IInitializable
    {
        private readonly IGameUnlockStateData _unlockState;
        private readonly RecipeViewerItemCatalog _itemCatalog;
        private readonly UnlockedMachineRecipeSource _machineRecipes;
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly IPlayerInventoryOperations _inventory;
        private readonly RecipeItemSelection _selection;
        private readonly UiRoutingState _routing;
        private readonly ReactiveProperty<ItemListViewModel> _itemList = new(ItemListViewModel.Empty);
        private readonly ReactiveProperty<RecipeViewerViewModel> _viewer = new(RecipeViewerNoSelection.Instance);
        private IReadOnlyList<CraftRecipeData> _unlockedCrafts;
        private IReadOnlyList<ItemId> _visibleItems;
        private bool _screenShown;

        public IReadOnlyReactiveProperty<ItemListViewModel> ItemList => _itemList;
        public IReadOnlyReactiveProperty<RecipeViewerViewModel> Viewer => _viewer;

        public RecipeScreenModel(IGameUnlockStateData unlockState, RecipeViewerItemCatalog itemCatalog, UnlockedMachineRecipeSource machineRecipes, IVanillaApiEvent vanillaApiEvent, IPlayerInventoryOperations inventory, RecipeItemSelection selection, UiRoutingState routing)
        {
            _unlockState = unlockState;
            _itemCatalog = itemCatalog;
            _machineRecipes = machineRecipes;
            _vanillaApiEvent = vanillaApiEvent;
            _inventory = inventory;
            _selection = selection;
            _routing = routing;
        }

        public void Initialize()
        {
            // 2 つの列は解放でしか変わらないので、ここと解放イベントでだけ組む
            // The two lists change only on unlocks, so build them here and on the unlock event only
            RebuildUnlockedLists();
            Rebuild();

            // 解放イベントは Initialize で購読する。VContainer は全 IInitializable を作ってから Initialize を回すので、ClientGameUnlockStateData の ctor の購読が先に走り更新後の表を読める（前例 ResearchNodeStateStore・UnlockedMachineRecipeSource）
            // Subscribe to unlock events in Initialize; VContainer builds every IInitializable first, so ClientGameUnlockStateData's ctor subscription runs earlier and the table is updated (precedent ResearchNodeStateStore, UnlockedMachineRecipeSource)
            _vanillaApiEvent.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => OnRecipesUnlocked());

            // 持ち物・機械レシピ・選択・言語の変化は表示中だけ組み直す（言語は素材の名前と秒数を View が描き直すため。持ち物は前例 ResearchTreeModel と同じ合図）
            // Inventory, machine recipe, selection and language changes rebuild only while shown (language so the View redraws names and seconds; inventory uses the same signal as ResearchTreeModel)
            _inventory.OnSnapshotChanged.Subscribe(_ => RebuildWhileShown());
            _machineRecipes.Current.Skip(1).Subscribe(_ => RebuildWhileShown());
            _selection.Current.Skip(1).Subscribe(_ => RebuildWhileShown());
            Localize.OnLanguageChanged.Subscribe(_ => RebuildWhileShown());
            _routing.Current.Subscribe(OnScreenChanged);

            #region Internal

            void OnRecipesUnlocked()
            {
                RebuildUnlockedLists();
                RebuildWhileShown();
            }

            void OnScreenChanged(UiRoutingSnapshot snapshot)
            {
                // 表示の突入で 1 回組む（閉じている間に捨てた変化をここで取り込む）
                // Build once on entering the screen (picks up the changes dropped while closed)
                _screenShown = snapshot.Screen == UiScreen.PlayerInventory;
                RebuildWhileShown();
            }

            #endregion
        }

        // 閉画面の変化は捨てる（前例 ResearchNodeStateStore.RefreshWhileResearchScreenActive）
        // Drop changes while closed (precedent ResearchNodeStateStore.RefreshWhileResearchScreenActive)
        private void RebuildWhileShown()
        {
            if (!_screenShown) return; // silent-ok: 閉画面では組まない / 次の表示の突入で最新を組み、録画 R-13 の再度開く段が見る
            Rebuild();
        }

        private void RebuildUnlockedLists()
        {
            _unlockedCrafts = CraftRecipeCatalog.BuildUnlocked(_unlockState);
            _visibleItems = _itemCatalog.BuildVisibleItems();
        }

        private void Rebuild()
        {
            // 持ち数はメインの枠だけで数える（Web の inventory.mainSlots）
            // Owned counts use only the main slots (the Web inventory.mainSlots)
            var owned = OwnedItemCounts.Build(_inventory.ReadSnapshot().MainSlots);
            _itemList.Value = RecipeViewModelFactory.BuildItemList(_visibleItems, _unlockedCrafts, owned);
            _viewer.Value = RecipeViewModelFactory.BuildViewer(_selection.Current.Value, _unlockedCrafts, _machineRecipes.Current.Value, owned);
        }
    }
}

using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Ui.State.BuildMenu.Entries;
using Cysharp.Threading.Tasks;
using Game.Construction;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BuildMenu.Source
{
    /// <summary>
    /// ビルドメニューの Model。開いている間だけ源の変化で組み直して Current で配る
    /// Build-menu model; while the menu is open, rebuilds on source changes and publishes through Current
    /// </summary>
    public sealed class BuildMenuModel : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly PlacementTargetResolver _placementTargetResolver;
        private readonly ConstructionWalletQuery _walletQuery;
        private readonly LocalPlayerInventoryController _inventoryController;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly IBlueprintThumbnailLookup _thumbnails;
        private readonly ReactiveProperty<BuildMenuViewModel> _current = new(BuildMenuViewModel.Empty);
        private bool _rebuildScheduled;

        public IReadOnlyReactiveProperty<BuildMenuViewModel> Current => _current;

        public BuildMenuModel(UIStateControl uiStateControl, PlacementTargetResolver placementTargetResolver, ConstructionWalletQuery walletQuery, LocalPlayerInventoryController inventoryController, ClientBlueprintLibrary blueprintLibrary, IBlueprintThumbnailLookup thumbnails)
        {
            _uiStateControl = uiStateControl;
            _placementTargetResolver = placementTargetResolver;
            _walletQuery = walletQuery;
            _inventoryController = inventoryController;
            _blueprintLibrary = blueprintLibrary;
            _thumbnails = thumbnails;
        }

        public void Initialize()
        {
            // 開いたときは BP ライブラリを取り直してから組む（Web の橋 BuildMenuTopic.OnStateChanged と同じ）
            // On open, refresh the blueprint library and rebuild (same as the Web bridge BuildMenuTopic.OnStateChanged)
            _uiStateControl.OnStateChanged.Where(state => state == UIStateEnum.BuildMenu).Subscribe(_ =>
            {
                _blueprintLibrary.Refresh(CancellationToken.None).Forget();
                ScheduleRebuild();
            });

            // 源の変化は開いている間だけ組み直す。同じフレームの多発はフレーム末の 1 回に畳む（橋 BuildMenuTopic.SchedulePublish と同じ。D-19）
            // Source changes rebuild only while open; bursts within a frame fold into one end-of-frame rebuild (same as the bridge's SchedulePublish; D-19)
            _blueprintLibrary.OnChanged.Subscribe(_ => RebuildWhileOpen());
            _thumbnails.OnThumbnailChanged.Subscribe(_ => RebuildWhileOpen());
            _walletQuery.OnWalletChanged.Subscribe(_ => RebuildWhileOpen());
            _inventoryController.LocalPlayerInventory.OnItemChange.Subscribe(_ => RebuildWhileOpen());
            _inventoryController.OnInventoryRefreshed.Subscribe(_ => RebuildWhileOpen());
            Localize.OnLanguageChanged.Subscribe(_ => RebuildWhileOpen());
        }

        private void RebuildWhileOpen()
        {
            // silent-ok: 閉じている間の変化は捨てる / 次に開いたとき Initialize の購読が組み直す
            // silent-ok: changes while closed are dropped / the next open rebuilds through the Initialize subscription
            if (_uiStateControl.CurrentState != UIStateEnum.BuildMenu) return;
            ScheduleRebuild();
        }

        private void ScheduleRebuild()
        {
            // silent-ok: 予約済みなら同じフレーム末の 1 回が最新の源で組む / 取りこぼしは無い
            // silent-ok: an already scheduled rebuild reads the latest sources at end of frame / nothing is lost
            if (_rebuildScheduled) return;
            _rebuildScheduled = true;
            RebuildAtEndOfFrame().Forget();

            #region Internal

            async UniTaskVoid RebuildAtEndOfFrame()
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                _rebuildScheduled = false;
                Rebuild();
            }

            void Rebuild()
            {
                var entries = BuildMenuEntryFactory.CreateEntries(_placementTargetResolver.CreateUnlockedTargets(), _walletQuery, _inventoryController.LocalPlayerInventory, _thumbnails);
                _current.Value = new BuildMenuViewModel(BuildMenuEntryFactory.CreateCategories(), BuildMenuEntryFactory.CreateHeadingNames(), entries);
            }

            #endregion
        }
    }
}

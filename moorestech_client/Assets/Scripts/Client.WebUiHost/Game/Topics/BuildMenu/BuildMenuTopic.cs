using System;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using Game.Construction;
using UniRx;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// build_menu.entries トピック: ビルドメニューのエントリ一覧を push
    /// build_menu.entries topic: pushes the build-menu entry list
    /// </summary>
    public class BuildMenuTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "build_menu.entries";

        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly PlacementTargetResolver _placementTargetResolver;
        private readonly ConstructionWalletQuery _constructionWalletQuery;
        private readonly LocalPlayerInventoryController _inventoryController;
        private readonly BuildMenuInventoryRepublishGate _republishGate;
        private readonly IDisposable _librarySubscription;
        private readonly IDisposable _remainingSubscription;
        private readonly IDisposable _inventorySubscription;
        private bool _publishScheduled;
        private bool _disposed;

        public BuildMenuTopic(WebSocketHub hub, UIStateControl uiStateControl, ClientBlueprintLibrary blueprintLibrary, PlacementTargetResolver placementTargetResolver, ConstructionWalletQuery constructionWalletQuery, LocalPlayerInventoryController inventoryController)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _blueprintLibrary = blueprintLibrary;
            _placementTargetResolver = placementTargetResolver;
            _constructionWalletQuery = constructionWalletQuery;
            _inventoryController = inventoryController;
            _republishGate = new BuildMenuInventoryRepublishGate(uiStateControl);

            // 入場・BP更新・残数変化で再配信
            // Republish on entry, BP updates, and remaining-count changes
            _uiStateControl.OnStateChanged += OnStateChanged;
            _librarySubscription = _blueprintLibrary.OnChanged.Subscribe(_ => SchedulePublish());
            _remainingSubscription = _constructionWalletQuery.OnWalletChanged.Subscribe(_ => SchedulePublish());

            // 不足判定は所持数に依存するため、表示中の所持変化でも配り直す（前例 ResearchTopic）
            // Shortage depends on holdings, so republish on inventory moves while the menu is up (precedent: ResearchTopic)
            _inventorySubscription = new CompositeDisposable(
                inventoryController.LocalPlayerInventory.OnItemChange.Subscribe(_ => SchedulePublishWhileBuildMenuActive()),
                inventoryController.OnInventoryRefreshed.Subscribe(_ => SchedulePublishWhileBuildMenuActive()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _uiStateControl.OnStateChanged -= OnStateChanged;
            _librarySubscription.Dispose();
            _remainingSubscription.Dispose();
            _inventorySubscription.Dispose();
        }

        private void OnStateChanged(UIStateEnum state)
        {
            if (state != UIStateEnum.BuildMenu) return;

            // uGUIビュー非表示時のBP更新はここが担う（更新完了は OnChanged 経由で再配信される）
            // While the uGUI view is hidden, this refresh path keeps blueprints fresh (completion republishes via OnChanged)
            _blueprintLibrary.Refresh(CancellationToken.None).Forget();
            SchedulePublish();
        }

        // 閉じている間の所持変化は次の入場時の再配信で足りる
        // Inventory moves while the menu is closed are covered by the republish on the next entry
        private void SchedulePublishWhileBuildMenuActive()
        {
            if (!_republishGate.ShouldRepublish()) return;
            SchedulePublish();
        }

        // INFRA-7 デバウンス規約: 同フレーム多発でもフレーム末の最終状態だけ配信する
        // INFRA-7 debounce rule: publish only the frame-end final state even on same-frame bursts
        private void SchedulePublish()
        {
            if (_publishScheduled) return;
            _publishScheduled = true;
            PublishAtEndOfFrame().Forget();

            #region Internal

            async UniTaskVoid PublishAtEndOfFrame()
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                _publishScheduled = false;
                if (_disposed) return;
                _hub.Publish(TopicName, BuildJson());
            }

            #endregion
        }

        private string BuildJson()
        {
            var dto = new BuildMenuTopicDto
            {
                Categories = BuildMenuEntryDtoFactory.CreateCategoryDtos(),
                Entries = BuildMenuEntryDtoFactory.CreateDtos(_placementTargetResolver, _constructionWalletQuery, _inventoryController.LocalPlayerInventory),
            };
            return WebUiJson.Serialize(dto);
        }
    }
}

using System;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
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
        private readonly IGameUnlockStateData _unlockState;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly IDisposable _librarySubscription;
        private bool _publishScheduled;
        private bool _disposed;

        public BuildMenuTopic(WebSocketHub hub, UIStateControl uiStateControl, IGameUnlockStateData unlockState, ClientBlueprintLibrary blueprintLibrary)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _unlockState = unlockState;
            _blueprintLibrary = blueprintLibrary;

            // BuildMenu入場で再配信、BPライブラリ更新でも再配信する
            // Republish on BuildMenu entry and on blueprint-library updates
            _uiStateControl.OnStateChanged += OnStateChanged;
            _librarySubscription = _blueprintLibrary.OnChanged.Subscribe(_ => SchedulePublish());
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
        }

        private void OnStateChanged(UIStateEnum state)
        {
            if (state != UIStateEnum.BuildMenu) return;

            // uGUIビュー非表示時のBP更新はここが担う（更新完了は OnChanged 経由で再配信される）
            // While the uGUI view is hidden, this refresh path keeps blueprints fresh (completion republishes via OnChanged)
            _blueprintLibrary.Refresh(CancellationToken.None).Forget();
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
                Entries = BuildMenuEntryDtoFactory.CreateDtos(_unlockState, _blueprintLibrary),
            };
            return WebUiJson.Serialize(dto);
        }
    }
}

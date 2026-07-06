using System;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// ui_state.current トピック: 現在のUIStateを push（Web側画面ルーティングの正）
    /// ui_state.current topic: pushes the current UI state (source of truth for web-side routing)
    /// </summary>
    public class UiStateTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui_state.current";

        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private bool _publishScheduled;
        private bool _disposed;

        public UiStateTopic(WebSocketHub hub, UIStateControl uiStateControl)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;

            // state遷移を購読して push する
            // Subscribe to state transitions and push them
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
        }

        private void OnStateChanged(UIStateEnum state)
        {
            SchedulePublish();
        }

        // INFRA-7 デバウンス規約: 同フレーム多段遷移でもフレーム末の最終stateだけ配信する
        // INFRA-7 debounce rule: publish only the final state at frame end even on multi-hop transitions
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
            return WebUiJson.Serialize(new UiStateDto { State = _uiStateControl.CurrentState.ToString() });
        }
    }

    /// <summary>
    /// ui_state.current の配信 DTO
    /// Payload DTO for ui_state.current
    /// </summary>
    public class UiStateDto
    {
        public string State;
    }
}

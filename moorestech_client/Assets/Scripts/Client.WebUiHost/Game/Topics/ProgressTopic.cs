using System;
using Client.Game.InGame.UI.ProgressBar;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// ui.progress トピック: 進捗バーの表示状態と進捗値を push
    /// ui.progress topic: pushes the progress bar's visibility and progress value
    /// </summary>
    public class ProgressTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui.progress";

        private readonly WebSocketHub _hub;
        private readonly ProgressBarView _view;
        private readonly IDisposable _subscription;
        private bool _publishScheduled;
        private bool _disposed;

        public ProgressTopic(WebSocketHub hub, ProgressBarView view)
        {
            _hub = hub;
            _view = view;

            // Show/Hide/SetProgress の変化を購読して push する
            // Subscribe to Show/Hide/SetProgress changes and push them
            _subscription = _view.OnProgressChanged.Subscribe(_ => SchedulePublish());
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _subscription.Dispose();
        }

        // INFRA-7 デバウンス規約: 採掘中の毎フレーム SetProgress をフレーム末の1回に畳む
        // INFRA-7 debounce rule: fold per-frame SetProgress during mining into one publish per frame
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
            // uGUI バーに label 源が無いため null を出す
            // The uGUI bar has no label source, so emit null
            var dto = new ProgressDto
            {
                Visible = _view.IsShown,
                Progress = _view.CurrentProgress,
                Label = null,
            };
            return WebUiJson.Serialize(dto);
        }
    }

    /// <summary>
    /// ui.progress の配信 DTO
    /// Payload DTO for ui.progress
    /// </summary>
    public class ProgressDto
    {
        public bool Visible;
        public float Progress;
        public string Label;
    }
}

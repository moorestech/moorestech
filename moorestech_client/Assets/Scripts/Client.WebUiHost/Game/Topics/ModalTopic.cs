using System;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// ui.modal トピック: 現在保留中のモーダル要求（無ければ null）を push
    /// ui.modal topic: pushes the currently pending modal request (null when none)
    /// </summary>
    public class ModalTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui.modal";

        private readonly WebSocketHub _hub;
        private readonly WebUiModalService _service;
        private bool _publishScheduled;
        private bool _disposed;

        public ModalTopic(WebSocketHub hub, WebUiModalService service)
        {
            _hub = hub;
            _service = service;

            // 保留要求の増減を購読して push する
            // Subscribe to pending-request changes and push them
            _service.OnPendingChanged += SchedulePublish;
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _service.OnPendingChanged -= SchedulePublish;
        }

        // INFRA-7 デバウンス規約: 同フレームで要求が置換されてもフレーム末の最終状態だけ配信する
        // INFRA-7 debounce rule: if requests are replaced within a frame, publish only the final state at frame end
        private void SchedulePublish()
        {
            if (_publishScheduled) return;
            _publishScheduled = true;
            PublishAtEndOfFrame().Forget();
        }

        private async UniTaskVoid PublishAtEndOfFrame()
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            _publishScheduled = false;
            if (_disposed) return;
            _hub.Publish(TopicName, BuildJson());
        }

        private string BuildJson()
        {
            var pending = _service.Pending;
            var dto = new ModalTopicDto
            {
                Modal = pending == null
                    ? null
                    : new ModalDto
                    {
                        Id = pending.Id,
                        Title = pending.Title,
                        Message = pending.Message,
                        ButtonText = pending.ButtonText,
                        Variant = pending.Variant,
                    },
            };
            return WebUiJson.Serialize(dto);
        }
    }

    /// <summary>
    /// ui.modal の配信 DTO
    /// Payload DTO for ui.modal
    /// </summary>
    public class ModalTopicDto
    {
        public ModalDto Modal;
    }

    public class ModalDto
    {
        public string Id;
        public string Title;
        public string Message;
        public string ButtonText;
        public string Variant;
    }
}

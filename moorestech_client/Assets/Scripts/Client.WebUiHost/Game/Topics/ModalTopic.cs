using System;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

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
        private readonly IDisposable _subscription;
        private bool _publishScheduled;
        private bool _disposed;

        public ModalTopic(WebSocketHub hub, WebUiModalService service)
        {
            _hub = hub;
            _service = service;

            // 保留要求の増減を購読して push する
            // Subscribe to pending-request changes and push them
            _subscription = _service.OnPendingChanged.Subscribe(_ => SchedulePublish());
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

        // INFRA-7 デバウンス規約: 同フレームで要求が置換されてもフレーム末の最終状態だけ配信する
        // INFRA-7 debounce rule: if requests are replaced within a frame, publish only the final state at frame end
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
                        // enum を web の文字列契約（"confirm" | "error"）へ境界で変換する
                        // Convert the enum to the web string contract ("confirm" | "error") at the boundary
                        Variant = pending.Variant == ModalVariant.Error ? "error" : "confirm",
                        // 入力モーダルのみ true を配信し、通常モーダルはキー省略する（既存フィクスチャ互換）
                        // Deliver true only for input modals; plain modals omit the key (keeps existing fixtures)
                        Input = pending.RequiresInput ? true : (bool?)null,
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
        public bool? Input;
    }
}

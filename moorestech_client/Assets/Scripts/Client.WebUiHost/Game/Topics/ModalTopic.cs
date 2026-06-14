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

        public ModalTopic(WebSocketHub hub, WebUiModalService service)
        {
            _hub = hub;
            _service = service;

            // 保留要求の増減を購読して push する
            // Subscribe to pending-request changes and push them
            _service.OnPendingChanged += OnPendingChanged;
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _service.OnPendingChanged -= OnPendingChanged;
        }

        private void OnPendingChanged()
        {
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

using System;
using Client.Game.InGame.UI.Tooltip;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class TooltipTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui.tooltip";
        private readonly WebSocketHub _hub;
        private readonly MouseCursorTooltip _tooltip;
        private readonly IDisposable _subscription;

        public TooltipTopic(WebSocketHub hub, MouseCursorTooltip tooltip)
        {
            _hub = hub;
            _tooltip = tooltip;
            _subscription = tooltip.OnPresentationChanged.Skip(1).Subscribe(_ => Publish());
        }

        public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(BuildJson());
        public void Dispose() => _subscription.Dispose();
        private void Publish() => _hub.Publish(TopicName, BuildJson());

        private string BuildJson()
        {
            var presentation = _tooltip.GetPresentation();
            return WebUiJson.Serialize(new TooltipDto
            {
                Visible = presentation.Visible,
                TextKey = presentation.TextKey,
                FontSize = presentation.FontSize,
            });
        }
    }

    public class TooltipDto
    {
        public bool Visible;
        public string TextKey;
        public int FontSize;
    }
}

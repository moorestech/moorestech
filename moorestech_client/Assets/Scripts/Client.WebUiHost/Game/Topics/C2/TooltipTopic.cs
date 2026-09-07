using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly MouseCursorTooltipState _tooltip;
        private readonly IDisposable _subscription;

        public TooltipTopic(WebSocketHub hub, MouseCursorTooltipState tooltip)
        {
            _hub = hub;
            _tooltip = tooltip;
            _subscription = tooltip.OnPresentationChanged.Skip(1).Subscribe(_ => Publish());
        }

        public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(BuildJson());
        public void Dispose() => _subscription.Dispose();
        private void Publish() => _hub.Publish(TopicName, BuildJson());

        private string BuildJson() => WebUiJson.Serialize(ToDto(_tooltip.GetPresentation()));

        // 行は常に配列で出す（非表示時も空配列）。Web側スキーマは lines 必須
        // Lines are always emitted as an array (empty when hidden); the web schema requires lines
        public static TooltipDto ToDto(TooltipPresentation presentation)
        {
            return new TooltipDto
            {
                Visible = presentation.Visible,
                Lines = presentation.Lines.Select(line => new TooltipLineDto { TextKey = line.Key.Key, TextParams = line.TextParams }).ToArray(),
            };
        }
    }

    public class TooltipDto
    {
        public bool Visible;
        public IReadOnlyList<TooltipLineDto> Lines;
    }

    public class TooltipLineDto
    {
        public string TextKey;
        public IReadOnlyList<string> TextParams;
    }
}

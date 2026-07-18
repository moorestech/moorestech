using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.ContextMenu;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class ContextMenuTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui.context_menu";
        private readonly WebSocketHub _hub;
        private readonly ContextMenuView _view;
        private readonly IDisposable _subscription;

        public ContextMenuTopic(WebSocketHub hub, ContextMenuView view)
        {
            _hub = hub;
            _view = view;
            _subscription = view.OnPresentationChanged.Subscribe(_ => _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(BuildJson());
        public void Dispose() => _subscription.Dispose();

        private string BuildJson()
        {
            var items = new List<ContextMenuItemDto>();
            var bars = _view.GetContextMenuBars();
            for (var i = 0; i < bars.Count; i++) items.Add(new ContextMenuItemDto { Id = i.ToString(), TitleKey = bars[i].Title });
            return WebUiJson.Serialize(new ContextMenuDto { Visible = _view.IsVisible(), Items = items });
        }
    }

    public class ContextMenuDto { public bool Visible; public List<ContextMenuItemDto> Items; }
    public class ContextMenuItemDto { public string Id; public string TitleKey; }
}

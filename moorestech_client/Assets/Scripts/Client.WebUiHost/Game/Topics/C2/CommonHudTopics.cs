using System;
using Client.Game.InGame.UI.Crosshair;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class CrosshairTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui.crosshair";
        private readonly WebSocketHub _hub;
        private readonly CrosshairView _view;
        private readonly IDisposable _subscription;

        public CrosshairTopic(WebSocketHub hub, CrosshairView view)
        {
            _hub = hub;
            _view = view;
            _subscription = view.OnVisibleChanged.Skip(1).Subscribe(_ => Publish());
        }

        public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(BuildJson());
        public void Dispose() => _subscription.Dispose();
        private void Publish() => _hub.Publish(TopicName, BuildJson());
        private string BuildJson() => WebUiJson.Serialize(new VisibilityDto { Visible = _view.IsVisible() });
    }

    public class UiVisibilityTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui.visibility";
        private readonly WebSocketHub _hub;
        private readonly UIRoot _root;
        private readonly IDisposable _subscription;

        public UiVisibilityTopic(WebSocketHub hub, UIRoot root)
        {
            _hub = hub;
            _root = root;
            _subscription = root.OnVisibilityChanged.Skip(1).Subscribe(_ => Publish());
        }

        public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(BuildJson());
        public void Dispose() => _subscription.Dispose();
        private void Publish() => _hub.Publish(TopicName, BuildJson());
        private string BuildJson() => WebUiJson.Serialize(new VisibilityDto { Visible = _root.IsVisible() });
    }

    public class VisibilityDto { public bool Visible; }
}

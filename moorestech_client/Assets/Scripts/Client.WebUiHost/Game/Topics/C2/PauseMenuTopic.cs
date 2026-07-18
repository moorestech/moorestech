using System;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class PauseMenuTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "pause_menu.current";

        private readonly WebSocketHub _hub;
        private readonly NetworkDisconnectPresenter _presenter;
        private readonly IDisposable _subscription;

        public PauseMenuTopic(WebSocketHub hub, NetworkDisconnectPresenter presenter)
        {
            _hub = hub;
            _presenter = presenter;

            // 切断状態の変化だけを配信し、再接続時はsnapshotから復元する
            // Publish only disconnect changes and restore from the snapshot after reconnect
            _subscription = presenter.OnDisconnectedChanged.Skip(1).Subscribe(_ => Publish());
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private void Publish()
        {
            _hub.Publish(TopicName, BuildJson());
        }

        private string BuildJson()
        {
            return WebUiJson.Serialize(new PauseMenuDto { Disconnected = _presenter.IsDisconnected });
        }
    }

    public class PauseMenuDto
    {
        public bool Disconnected;
    }
}

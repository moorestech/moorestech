using System;
using Client.Game.Common;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class GameStateTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "game_state.current";
        private readonly WebSocketHub _hub;
        private readonly IDisposable _subscription;

        public GameStateTopic(WebSocketHub hub)
        {
            _hub = hub;
            _subscription = GameStateController.OnStateChanged.Subscribe(_ => Publish());
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

        private static string BuildJson()
        {
            return WebUiJson.Serialize(new GameStateDto { State = GameStateController.CurrentState.ToString() });
        }
    }

    public class GameStateDto
    {
        public string State;
    }
}

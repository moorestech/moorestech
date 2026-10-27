using System;
using Client.Game.InGame.UI.Challenge;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class ChallengeTreeTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "challenge.tree";
        private readonly WebSocketHub _hub;
        private readonly ChallengeProgressState _state;
        private readonly IDisposable _subscription;

        public ChallengeTreeTopic(WebSocketHub hub, ChallengeProgressState state)
        {
            _hub = hub;
            _state = state;
            // 完了のたびに全カテゴリのツリーを配信する
            // Publish the whole category tree on every completion
            _subscription = _state.OnChallengeCompleted.Subscribe(_ => _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private string BuildJson()
        {
            return WebUiJson.Serialize(ChallengeDtoBuilder.BuildTree(_state.Categories));
        }
    }
}

using System;
using Client.Ui.State.Challenge;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class ChallengeCurrentTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "challenge.current";
        private readonly WebSocketHub _hub;
        private readonly ChallengeProgressState _state;
        private readonly IDisposable _subscription;

        public ChallengeCurrentTopic(WebSocketHub hub, ChallengeProgressState state)
        {
            _hub = hub;
            _state = state;
            // 完了GUIDはeventのみ
            // Completed GUID goes only in the event
            _subscription = _state.OnChallengeCompleted.Subscribe(completedGuid => _hub.Publish(TopicName, BuildJson(completedGuid.ToString())));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson(null));
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private string BuildJson(string completedGuid)
        {
            return WebUiJson.Serialize(ChallengeDtoBuilder.BuildCurrent(_state.Categories, completedGuid));
        }
    }
}

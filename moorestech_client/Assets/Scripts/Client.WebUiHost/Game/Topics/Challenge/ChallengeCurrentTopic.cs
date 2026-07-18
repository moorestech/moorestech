using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Game.Topics
{
    public class ChallengeCurrentTopic : ITopicHandler
    {
        public const string TopicName = "challenge.current";
        private readonly ChallengeTopicState _state;

        public ChallengeCurrentTopic(ChallengeTopicState state)
        {
            _state = state;
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(_state.BuildCurrentJson(null));
        }
    }
}

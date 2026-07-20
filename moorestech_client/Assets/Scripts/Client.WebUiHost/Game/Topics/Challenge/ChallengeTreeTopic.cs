using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Game.Topics
{
    public class ChallengeTreeTopic : ITopicHandler
    {
        public const string TopicName = "challenge.tree";
        private readonly ChallengeTopicState _state;

        public ChallengeTreeTopic(ChallengeTopicState state)
        {
            _state = state;
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(_state.BuildTreeJson());
        }
    }
}

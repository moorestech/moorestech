using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Game.Topics
{
    public class TutorialPresentationTopic : ITopicHandler
    {
        public const string TopicName = "tutorial.presentation";
        public static TutorialPresentationDto Current { get; } = new()
        {
            TutorialSessionId = "", Revision = 0, ChallengeId = "",
            Highlights = System.Array.Empty<TutorialHighlightDto>()
        };

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(WebUiJson.Serialize(Current));
        }
    }

    public class TutorialPresentationDto
    {
        public string TutorialSessionId;
        public int Revision;
        public string ChallengeId;
        public TutorialHighlightDto[] Highlights;
    }

    public class TutorialHighlightDto
    {
        public string HighlightId;
        public string AnchorId;
        public string Kind;
        public string Message;
        public int PaddingPx;
        public bool BlocksPointerInput;
    }
}

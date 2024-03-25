using Cysharp.Threading.Tasks;

namespace Client.Story.StoryTrack
{
    public class TransitionTrack : IStoryTrack
    {
        private const float DefaultDuration = 0.5f;
        public async UniTask ExecuteTrack(StoryContext storyContext, string[] parameters)
        {
            var isShow = parameters[1] == "true";
            storyContext.StoryUI.ShowTransition(isShow, DefaultDuration);
            
            await UniTask.Delay((int)(DefaultDuration * 1000));
        }
    }
}
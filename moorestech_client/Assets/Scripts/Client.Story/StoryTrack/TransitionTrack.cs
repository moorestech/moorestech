using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Story.StoryTrack
{
    public class TransitionTrack : IStoryTrack
    {
        private const float DefaultDuration = 0.5f;
        public async UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            var isShow = parameters[0] == "TRUE";
            storyContext.StoryUI.ShowTransition(isShow, DefaultDuration);
            
            await UniTask.Delay((int)(DefaultDuration * 1000));
            
            return null;
        }
    }
}
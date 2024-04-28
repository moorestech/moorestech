using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Skit.SkitTrack
{
    public class TransitionTrack : IStoryTrack
    {
        public async UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            var isShow = parameters[0] == "TRUE";
            var duration = float.Parse(parameters[1]);
            storyContext.SkitMainUI.ShowTransition(isShow, duration);

            await UniTask.Delay((int)(duration * 1000));

            return null;
        }
    }
}
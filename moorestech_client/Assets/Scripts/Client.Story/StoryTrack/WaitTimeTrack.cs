using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Story.StoryTrack
{
    public class WaitTimeTrack : IStoryTrack
    {
        public async UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            var time = float.Parse(parameters[0]);
            await UniTask.Delay((int)(time * 1000));
            
            return null;
        }
    }
}
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Story.StoryTrack
{
    public class WaitTimeTrack : IStoryTrack
    {
        public UniTask ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            var time = float.Parse(parameters[0]);
            return UniTask.Delay((int)(time * 1000));
        }
    }
}
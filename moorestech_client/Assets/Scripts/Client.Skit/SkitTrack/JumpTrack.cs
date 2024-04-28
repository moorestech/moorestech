using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Skit.SkitTrack
{
    public class JumpTrack : IStoryTrack
    {
        public async UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            return parameters[0];
        }
    }
}
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Story.StoryTrack
{
    public interface IStoryTrack
    {
        public UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters);
    }
}
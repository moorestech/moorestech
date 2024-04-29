using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Skit.SkitTrack
{
    public interface IStoryTrack
    {
        public UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters);
    }
}
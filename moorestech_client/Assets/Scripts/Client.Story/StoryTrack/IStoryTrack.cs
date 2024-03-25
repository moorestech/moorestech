using Cysharp.Threading.Tasks;

namespace Client.Story.StoryTrack
{
    public interface IStoryTrack
    {
        public UniTask ExecuteTrack(StoryContext storyContext,string[] parameters);
    }
}
using Cysharp.Threading.Tasks;

namespace Client.Story.StoryTrack
{
    public class WaitTimeTrack : IStoryTrack
    {
        public UniTask ExecuteTrack(StoryContext storyContext, string[] parameters)
        {
            var time = float.Parse(parameters[1]);
            return UniTask.Delay((int)(time * 1000));
        }
    }
}
using Client.Skit.SkitTrack;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class WaitCommand
    {
        public async UniTask<string> ExecuteAsync(StoryContext storyContext)
        {
            await UniTask.Delay((int)(Seconds * 1000));
            return null;
        }
    }
}

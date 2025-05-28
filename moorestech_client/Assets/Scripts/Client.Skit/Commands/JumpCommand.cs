using Client.Skit.SkitTrack;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class JumpCommand
    {
        public async UniTask<string> ExecuteAsync(StoryContext storyContext)
        {
            return TargetLabel;
        }
    }
}

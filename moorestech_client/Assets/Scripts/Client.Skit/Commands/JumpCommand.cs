using Client.Skit.SkitTrack;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class JumpCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            return new CommandResultContext
            {
                JumpTargetCommandId = JumpTargetCommand,
            };
        }
    }
}

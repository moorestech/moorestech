using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial interface ICommandForgeCommand
    {
        public UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext);
    }
    
    public class CommandResultContext
    {
        public CommandId JumpTargetCommandId;
    }
}
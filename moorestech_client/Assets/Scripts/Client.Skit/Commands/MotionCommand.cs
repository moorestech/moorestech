using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class MotionCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var character = storyContext.GetCharacter(Character);
            character.PlayAnimation(MotionName, 0);
            return null;
        }
    }
}

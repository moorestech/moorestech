using Client.Skit.SkitTrack;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class MotionCommand
    {
        public async UniTask<string> ExecuteAsync(StoryContext storyContext)
        {
            var character = storyContext.GetCharacter(Character);
            character.PlayAnimation(MotionName);
            return null;
        }
    }
}

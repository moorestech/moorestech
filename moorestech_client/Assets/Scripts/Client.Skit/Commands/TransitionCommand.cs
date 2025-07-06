using Client.Skit.Context;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class TransitionCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            storyContext.GetSkitUI().ShowTransition(Enabled, Duration);
            await UniTask.Delay((int)(Duration * 1000));
            return null;
        }
    }
}

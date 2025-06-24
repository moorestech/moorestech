using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class ShowTextCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            storyContext.SkitUI.ShowTextArea(Enable);
            return null;
        }
    }
}
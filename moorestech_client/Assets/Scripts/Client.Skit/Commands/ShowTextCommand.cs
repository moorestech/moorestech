using Client.Skit.Context;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class ShowTextCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            SkitPresentationStateStore.Instance.SetTextAreaVisible(Enable);
            return null;
        }
    }
}

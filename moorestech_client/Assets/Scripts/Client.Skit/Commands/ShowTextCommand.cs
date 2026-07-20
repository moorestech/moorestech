using Client.Skit.Context;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class ShowTextCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var mode = storyContext.GetService<SkitPresentationMode>();
            if (mode.WebUiEnabled)
                SkitPresentationStateStore.Instance.SetTextAreaVisible(Enable);
            else
                storyContext.GetSkitUI().ShowTextArea(Enable);
            return null;
        }
    }
}

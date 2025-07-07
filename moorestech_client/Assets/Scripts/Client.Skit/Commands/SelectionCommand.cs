using System.Collections.Generic;
using Client.Skit.Context;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class SelectionCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var skitUI = storyContext.GetSkitUI();
            skitUI.ShowSelectionUI(true);

            var jumpTarget = new List<CommandId>();
            var labels = new List<string>();

            if (!string.IsNullOrEmpty(Option1Tag))
            {
                jumpTarget.Add(Option1JumpTarget);
                labels.Add(Option1Tag);
            }
            if (!string.IsNullOrEmpty(Option2Tag) && Option2JumpTarget.HasValue)
            {
                jumpTarget.Add(Option2JumpTarget.Value);
                labels.Add(Option2Tag);
            }
            if (!string.IsNullOrEmpty(Option3Tag) && Option3JumpTarget.HasValue)
            {
                jumpTarget.Add(Option3JumpTarget.Value);
                labels.Add(Option3Tag);
            }

            var index = await skitUI.WaitSelectText(labels);
            skitUI.ShowSelectionUI(false);
            
            return null; // TODO
        }
    }
}

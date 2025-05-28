using System.Collections.Generic;
using Client.Skit.SkitTrack;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class SelectionCommand
    {
        public async UniTask<string> ExecuteAsync(StoryContext storyContext)
        {
            storyContext.SkitUI.ShowSelectionUI(true);

            var jumpTags = new List<string>();
            var labels = new List<string>();

            if (!string.IsNullOrEmpty(Option1Label))
            {
                jumpTags.Add(Option1Tag);
                labels.Add(Option1Label);
            }
            if (!string.IsNullOrEmpty(Option2Label))
            {
                jumpTags.Add(Option2Tag);
                labels.Add(Option2Label);
            }
            if (!string.IsNullOrEmpty(Option3Label))
            {
                jumpTags.Add(Option3Tag);
                labels.Add(Option3Label);
            }

            var index = await storyContext.SkitUI.WaitSelectText(labels);
            storyContext.SkitUI.ShowSelectionUI(false);

            return index < jumpTags.Count ? jumpTags[index] : null;
        }
    }
}

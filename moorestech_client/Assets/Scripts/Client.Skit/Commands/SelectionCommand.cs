using System.Collections.Generic;
using Client.Skit.Context;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class SelectionCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var skitUI = storyContext.GetSkitUI();
            var presentationMode = storyContext.GetService<SkitPresentationMode>();
            if (!presentationMode.WebUiEnabled) skitUI.ShowSelectionUI(true);

            var jumpTarget = new List<CommandId>();
            var labels = new List<string>();
            var choices = new List<SkitChoice>();

            // optional optionを表示順どおりjumpとopaque IDへ同時展開する
            // Expand optional options into jumps and opaque IDs in the same display order
            if (!string.IsNullOrEmpty(Option1Tag))
            {
                jumpTarget.Add(Option1JumpTarget);
                labels.Add(Option1Tag);
                choices.Add(CreateChoice(Option1Tag));
            }
            if (!string.IsNullOrEmpty(Option2Tag) && Option2JumpTarget.HasValue)
            {
                jumpTarget.Add(Option2JumpTarget.Value);
                labels.Add(Option2Tag);
                choices.Add(CreateChoice(Option2Tag));
            }
            if (!string.IsNullOrEmpty(Option3Tag) && Option3JumpTarget.HasValue)
            {
                jumpTarget.Add(Option3JumpTarget.Value);
                labels.Add(Option3Tag);
                choices.Add(CreateChoice(Option3Tag));
            }

            // WebはchoiceIdだけを返し、Unity側で同じindexのjumpを確定する
            // Web returns only a choiceId; Unity resolves the jump at the matching index
            int index;
            if (presentationMode.WebUiEnabled)
            {
                var store = SkitPresentationStateStore.Instance;
                store.PresentChoices(choices.ToArray());
                var choiceId = await store.WaitForSelectionAsync();
                index = SkitChoiceJumpResolver.ResolveSelectedIndex(choiceId, choices.ToArray());
            }
            else
            {
                index = await skitUI.WaitSelectText(labels);
                skitUI.ShowSelectionUI(false);
            }
            return new CommandResultContext { JumpTargetCommandId = jumpTarget[index] };

            #region Internal

            SkitChoice CreateChoice(string label)
            {
                return new SkitChoice { ChoiceId = System.Guid.NewGuid().ToString(), Label = label };
            }

            #endregion
        }
    }
}

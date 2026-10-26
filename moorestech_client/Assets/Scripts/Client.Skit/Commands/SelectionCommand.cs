using System.Collections.Generic;
using Client.Skit.Context;
using Client.Skit.Localization;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class SelectionCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {

            var jumpTarget = new List<CommandId>();
            var choices = new List<SkitChoice>();
            var resolver = storyContext.GetLocalizationResolver();
            var commandId = (int)CommandId;

            // optional optionを表示順どおりjumpとopaque IDへ同時展開する
            // Expand optional options into jumps and opaque IDs in the same display order
            if (!string.IsNullOrEmpty(Option1Tag))
            {
                var label = SkitCommandLocalization.ResolveOption1(
                    resolver, commandId, Option1Tag);
                jumpTarget.Add(Option1JumpTarget);
                choices.Add(CreateChoice(label));
            }
            if (!string.IsNullOrEmpty(Option2Tag) && Option2JumpTarget.HasValue)
            {
                var label = SkitCommandLocalization.ResolveOption2(
                    resolver, commandId, Option2Tag);
                jumpTarget.Add(Option2JumpTarget.Value);
                choices.Add(CreateChoice(label));
            }
            if (!string.IsNullOrEmpty(Option3Tag) && Option3JumpTarget.HasValue)
            {
                var label = SkitCommandLocalization.ResolveOption3(
                    resolver, commandId, Option3Tag);
                jumpTarget.Add(Option3JumpTarget.Value);
                choices.Add(CreateChoice(label));
            }

            // WebはchoiceIdだけを返し、Unity側で同じindexのjumpを確定する
            // Web returns only a choiceId; Unity resolves the jump at the matching index
            var store = SkitPresentationStateStore.Instance;
            store.PresentChoices(choices.ToArray());
            var choiceId = await store.WaitForSelectionAsync();
            var index = SkitChoiceJumpResolver.ResolveSelectedIndex(choiceId, choices.ToArray());
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

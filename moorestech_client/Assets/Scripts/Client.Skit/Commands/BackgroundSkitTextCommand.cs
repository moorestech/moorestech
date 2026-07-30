using Client.Skit.Context;
using Client.Skit.Localization;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class BackgroundSkitTextCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var resolver = storyContext.GetLocalizationResolver();
            var skitTitle = storyContext.GetExecutionIdentity().SkitTitle;
            var commandId = (int)CommandId;
            var localizedBody = resolver.ResolveCommandField(skitTitle, commandId, "body", Body);
            var characterName = resolver.ResolveCharacterName(
                CharacterId,
                skitTitle,
                commandId,
                IsOverrideCharacterName,
                OverrideCharacterName);
            
            // WebとuGUIへ同じ解決済み文字列を渡す
            // Pass the same resolved display strings to Web and uGUI
            var skitUi = storyContext.GetBackgroundSkitUI();
            SkitPresentationStateStore.Instance.SetBackgroundText(characterName, localizedBody);
            skitUi.SetText(characterName, localizedBody);
            
            var voiceClip = storyContext.GetVoiceDefine().GetVoiceClip(CharacterId, Body);
            await skitUi.PlayVoiceAndWait(voiceClip);
            
            return null;
        }
    }
}

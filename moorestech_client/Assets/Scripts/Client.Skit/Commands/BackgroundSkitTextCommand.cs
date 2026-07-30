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
            var commandId = (int)CommandId;
            var line = SkitCommandLocalization.ResolveLine(
                resolver,
                storyContext.GetExecutionIdentity(),
                commandId,
                CharacterId,
                IsOverrideCharacterName,
                OverrideCharacterName,
                Body);
            
            // 解決文をWeb・uGUIへ共有
            // Share resolved text with Web and uGUI
            var skitUi = storyContext.GetBackgroundSkitUI();
            SkitPresentationStateStore.Instance.SetBackgroundText(
                line.SpeakerName,
                line.DisplayBody);
            skitUi.SetText(line.SpeakerName, line.DisplayBody);
            
            var voiceClip = storyContext.GetVoiceDefine()
                .GetVoiceClip(CharacterId, line.VoiceSourceBody);
            await skitUi.PlayVoiceAndWait(voiceClip);
            
            return null;
        }
    }
}

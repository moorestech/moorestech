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
                commandId,
                CharacterId,
                IsOverrideCharacterName,
                OverrideCharacterName,
                Body);
            
            // 解決文を表示側（UITK）へ共有
            // Share the resolved text with the display side (UITK)
            var voicePlayer = storyContext.GetBackgroundSkitVoicePlayer();
            SkitPresentationStateStore.Instance.SetBackgroundText(
                line.SpeakerName,
                line.DisplayBody);

            var voiceClip = storyContext.GetVoiceDefine()
                .GetVoiceClip(CharacterId, line.VoiceSourceBody);
            await voicePlayer.PlayVoiceAndWait(voiceClip);
            
            return null;
        }
    }
}

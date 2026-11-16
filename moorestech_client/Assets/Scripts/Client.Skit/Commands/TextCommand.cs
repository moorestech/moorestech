using System;
using Client.Skit.Context;
using Client.Skit.Localization;
using Client.Skit.Skit;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class TextCommand
    {
        private const float SkipDuration = 0.1f;
        
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var resolver = storyContext.GetLocalizationResolver();
            var commandId = (int)CommandId;
            var useOverride = IsOverrideCharacterName.HasValue && IsOverrideCharacterName.Value;
            var line = SkitCommandLocalization.ResolveLine(
                resolver,
                commandId,
                CharacterId,
                useOverride,
                OverrideCharacterName,
                Body);

            // 表示文字列だけを解決し、音声照合はJSON原文を維持する
            // Resolve display text only while voice lookup keeps the JSON source body
            var skitActionContext = storyContext.GetService<ISkitActionContext>();

            // 全文snapshotをpushし、Unity側のintentを待つ
            // Push the full snapshot and wait for a Unity-owned intent
            return await ExecuteWebPresentationAsync(
                line.SpeakerName,
                line.DisplayBody,
                skitActionContext);

            #region Internal

            async UniTask<CommandResultContext> ExecuteWebPresentationAsync(
                string speakerName,
                string displayBody,
                ISkitActionContext actionContext)
            {
                var store = SkitPresentationStateStore.Instance;
                store.PresentBlockingText(speakerName, displayBody);
                var advanceWait = store.WaitForAdvanceAsync();

                // ボイスは従来どおりUnity AudioSourceで再生する
                // Keep voice playback on the existing Unity AudioSource path
                var clip = storyContext.GetVoiceDefine()
                    .GetVoiceClip(CharacterId, line.VoiceSourceBody);
                var skitCharacter = storyContext.GetCharacter(CharacterId);
                if (clip != null) skitCharacter.PlayVoice(clip);

                if (actionContext.IsSkip)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(SkipDuration));
                    var current = store.GetCurrent();
                    store.TryAdvance(current.SessionId, current.SceneRevision);
                }

                await advanceWait;
                return null;
            }

            #endregion
        }
    }
}

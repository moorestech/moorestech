using System;
using System.Threading;
using Client.Skit.Context;
using Client.Skit.Localization;
using Client.Skit.Skit;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public partial class TextCommand
    {
        private const float TextDuration = 0.05f;
        private const float WaitDuration = 1f;
        private const float SkipDuration = 0.1f;
        
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var resolver = storyContext.GetLocalizationResolver();
            var skitTitle = storyContext.GetExecutionIdentity().SkitTitle;
            var commandId = (int)CommandId;
            var localizedBody = resolver.ResolveCommandField(skitTitle, commandId, "body", Body);
            var useOverride = IsOverrideCharacterName.HasValue && IsOverrideCharacterName.Value;
            var characterName = resolver.ResolveCharacterName(
                CharacterId,
                skitTitle,
                commandId,
                useOverride,
                OverrideCharacterName);

            // 表示文字列だけを解決し、音声照合はJSON原文を維持する
            // Resolve display text only while voice lookup keeps the JSON source body
            var skitUi = storyContext.GetSkitUI();
            var skitActionContext = storyContext.GetService<ISkitActionContext>();
            var presentationMode = storyContext.GetService<SkitPresentationMode>();

            // Webモードでは全文snapshotをpushし、Unity側のintent待機だけを行う
            // In Web mode, push the full snapshot and wait only for a Unity-owned intent
            if (presentationMode.WebUiEnabled)
            {
                return await ExecuteWebPresentationAsync(
                    characterName,
                    localizedBody,
                    skitActionContext);
            }
            
            if (skitActionContext.IsSkip)
            {
                skitUi.SetText(characterName, localizedBody);
                await UniTask.Delay(TimeSpan.FromSeconds(SkipDuration));
                return null;
            }
            
            var setTextTaskCancellationTokenSource = new CancellationTokenSource();
            UniTask<bool> setTextTask = UniTask.Create(factory: async () =>
            {
                skitUi.SetText(characterName, "");
                
                for (var i = 0; i < localizedBody.Length; i++)
                {
                    var bodySlice = localizedBody.Substring(0, i + 1);
                    skitUi.SetText(characterName, bodySlice);
                    await UniTask.Delay(TimeSpan.FromSeconds(TextDuration), cancellationToken: setTextTaskCancellationTokenSource.Token);
                }
            }).SuppressCancellationThrow();
            
            var voiceClip = storyContext.GetVoiceDefine().GetVoiceClip(CharacterId, Body);
            var character = storyContext.GetCharacter(CharacterId);
            
            if (voiceClip != null) character.PlayVoice(voiceClip);
            
            // 文字送りを止め、全てを表示する
            while (setTextTask.Status != UniTaskStatus.Succeeded && setTextTask.Status != UniTaskStatus.Canceled && setTextTask.Status != UniTaskStatus.Faulted && !setTextTaskCancellationTokenSource.IsCancellationRequested)
            {
                if (Input.GetMouseButtonDown(0) || skitActionContext.IsSkip)
                {
                    setTextTaskCancellationTokenSource.Cancel();
                    await UniTask.Yield();
                    skitUi.SetText(characterName, localizedBody);
                    character.StopVoice();
                    break;
                }
                await UniTask.Yield();
            }
            
            // 次のコマンドへ移行
            var waitStartTime = Time.timeAsDouble;
            while (true)
            {
                if (skitActionContext.IsAuto && Time.timeAsDouble - waitStartTime >= WaitDuration || Input.GetMouseButtonDown(0) || skitActionContext.IsSkip)
                {
                    await UniTask.Yield();
                    return null;
                }
                
                await UniTask.Yield();
            }

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
                var clip = storyContext.GetVoiceDefine().GetVoiceClip(CharacterId, Body);
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

using System;
using System.Threading;
using Client.Skit.Context;
using Client.Skit.Skit;
using Core.Master;
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
            var characterName = MasterHolder.CharacterMaster.GetCharacterMaster(CharacterId).DisplayName;
            if (IsOverrideCharacterName.HasValue && IsOverrideCharacterName.Value)
            {
                characterName = OverrideCharacterName;
            }
            
            var skitUi = storyContext.GetSkitUI();
            var skitActionContext = storyContext.GetService<ISkitActionContext>();
            
            if (skitActionContext.IsSkip)
            {
                skitUi.SetText(characterName, Body);
                await UniTask.Delay(TimeSpan.FromSeconds(SkipDuration));
                return null;
            }
            
            var setTextTaskCancellationTokenSource = new CancellationTokenSource();
            UniTask<bool> setTextTask = UniTask.Create(factory: async () =>
            {
                skitUi.SetText(characterName, "");
                
                for (var i = 0; i < Body.Length; i++)
                {
                    var bodySlice = Body.Substring(0, i + 1);
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
                    skitUi.SetText(characterName, Body);
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
        }
    }
}
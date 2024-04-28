using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Skit.SkitTrack
{
    public class TextTrack : IStoryTrack
    {
        public async UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            var characterName = parameters[0];
            var text = parameters[1];

            storyContext.SkitMainUI.SetText(characterName, text);

            var voiceAudioClip = storyContext.VoiceDefine.GetVoiceClip(characterName, text);
            if (voiceAudioClip != null)
            {
                var character = storyContext.GetCharacter(characterName);
                character.PlayVoice(voiceAudioClip);
            }

            //クリックされるまで待機
            while (true)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    // 1フレーム待たないとクリックが即座に次のテキストに反映されてしまう
                    await UniTask.Yield();
                    return null;
                }
                await UniTask.Yield();
            }
        }
    }
}
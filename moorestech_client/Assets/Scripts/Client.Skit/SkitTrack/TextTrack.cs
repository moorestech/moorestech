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
            var characterDisplayName = parameters[2] == string.Empty ? characterName : parameters[2];
            var text = parameters[1];

            storyContext.SkitUI.SetText(characterDisplayName, text);

            var voiceAudioClip = storyContext.VoiceDefine.GetVoiceClip(characterName, text);

            var character = storyContext.GetCharacter(characterName);
            if (voiceAudioClip != null)
            {
                character.PlayVoice(voiceAudioClip);
            }

            //クリックされるまで待機
            while (true)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    // 1フレーム待たないとクリックが即座に次のテキストに反映されてしまう
                    await UniTask.Yield();
                    character.StopVoice();
                    return null;
                }
                await UniTask.Yield();
            }
        }
    }
}
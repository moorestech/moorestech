using Client.Skit.SkitTrack;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public partial class TextCommand
    {
        public async UniTask<string> ExecuteAsync(StoryContext storyContext)
        {
            storyContext.SkitUI.SetText(Character, Body);

            var voiceClip = storyContext.VoiceDefine.GetVoiceClip(Character, Body);
            var character = storyContext.GetCharacter(Character);
            if (voiceClip != null) character.PlayVoice(voiceClip);

            while (true)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    await UniTask.Yield();
                    character.StopVoice();
                    return null;
                }
                await UniTask.Yield();
            }
        }
    }
}

using Client.Skit.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public partial class TextCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var characterName = MasterHolder.CharacterMaster.GetCharacterMaster(CharacterId).DisplayName;
            if (IsOverrideCharacterName)
            {
                characterName = OverrideCharacterName;
            }
            
            var skitUi = storyContext.GetSkitUI();
            skitUi.SetText(characterName, Body);
            
            var voiceClip = storyContext.GetVoiceDefine().GetVoiceClip(CharacterId, Body);
            var character = storyContext.GetCharacter(CharacterId);
            
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

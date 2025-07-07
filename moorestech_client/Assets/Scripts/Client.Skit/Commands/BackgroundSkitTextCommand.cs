using Client.Skit.Context;
using Core.Master;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class BackgroundSkitTextCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var characterName = MasterHolder.CharacterMaster.GetCharacterMaster(CharacterId).DisplayName;
            if (IsOverrideCharacterName)
            {
                characterName = OverrideCharacterName;
            }
            
            var skitUi = storyContext.GetBackgroundSkitUI();
            
            var voiceClip = storyContext.GetVoiceDefine().GetVoiceClip(CharacterId, Body);
            await skitUi.SetText(characterName, Body, voiceClip);
            
            return null;
        }
    }
}

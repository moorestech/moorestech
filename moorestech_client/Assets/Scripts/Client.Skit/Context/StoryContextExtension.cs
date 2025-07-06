using Client.Skit.Define;
using Client.Skit.Skit;
using Client.Skit.UI;

namespace Client.Skit.Context
{
    public static class StoryContextExtension
    {
        public static SkitUI GetSkitUI(this StoryContext storyContext) => storyContext.GetService<SkitUI>();
        
        public static VoiceDefine GetVoiceDefine(this StoryContext storyContext) => storyContext.GetService<VoiceDefine>();
        
        public static ISkitCamera GetSkitCamera(this StoryContext storyContext) => storyContext.GetService<ISkitCamera>();
        
        public static SkitCharacter GetCharacter(this StoryContext storyContext, string characterId)
        {
            var characterObjectContainer = storyContext.GetService<CharacterObjectContainer>();
            return characterObjectContainer.GetCharacter(characterId);
        }
    }
}
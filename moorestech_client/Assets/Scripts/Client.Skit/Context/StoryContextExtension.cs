using Client.Skit.Define;
using Client.Skit.UI;

namespace Client.Skit.Context
{
    public static class StoryContextExtension
    {
        public static SkitUI GetSkitUI(this StoryContext storyContext) => storyContext.GetService<SkitUI>();
        public static VoiceDefine 
    }
}
using Cysharp.Threading.Tasks;

namespace Client.Story.StoryTrack
{
    public class CharacterMotionTrack : IStoryTrack
    {
        public async UniTask ExecuteTrack(StoryContext storyContext, string[] parameters)
        {
            var characterKey = parameters[1];
            var animationName = parameters[2];
            
            var character = storyContext.GetCharacter(characterKey);
            character.PlayAnimation(animationName);
        }
    }
}
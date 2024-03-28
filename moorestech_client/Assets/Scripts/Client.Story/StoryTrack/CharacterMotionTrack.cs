using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Story.StoryTrack
{
    public class CharacterMotionTrack : IStoryTrack
    {
        public async UniTask ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            var characterKey = parameters[0];
            var animationName = parameters[1];
            
            var character = storyContext.GetCharacter(characterKey);
            character.PlayAnimation(animationName);
        }
    }
}
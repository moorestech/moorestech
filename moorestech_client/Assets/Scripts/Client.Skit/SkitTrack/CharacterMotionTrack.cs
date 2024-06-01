using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Skit.SkitTrack
{
    public class CharacterMotionTrack : IStoryTrack
    {
        public async UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            var characterKey = parameters[0];
            var animationName = parameters[1];

            var character = storyContext.GetCharacter(characterKey);
            character.PlayAnimation(animationName);

            return null;
        }
    }
}
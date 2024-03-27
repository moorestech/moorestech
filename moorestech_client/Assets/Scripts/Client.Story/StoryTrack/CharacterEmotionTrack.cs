using Cysharp.Threading.Tasks;
using UnityEngine;
using static System.Enum;

namespace Client.Story.StoryTrack
{
    public class CharacterEmotionTrack : IStoryTrack
    {
        public async UniTask ExecuteTrack(StoryContext storyContext, string[] parameters)
        {
            var characterKey = parameters[1];
            var emotion = (EmotionType)Parse(typeof(EmotionType), parameters[2]);
            var duration = float.Parse(parameters[3]);

            var character = storyContext.GetCharacter(characterKey);
            character.SetEmotion(emotion, duration);
        }
    }
}
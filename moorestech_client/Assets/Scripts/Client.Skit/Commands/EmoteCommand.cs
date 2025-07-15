using Client.Skit.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static System.Enum;

namespace CommandForgeGenerator.Command
{
    public partial class EmoteCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var character = storyContext.GetCharacter(Character);
            character.SetEmotion(Emotion, Duration, Weight);
            return null;
        }
    }
}

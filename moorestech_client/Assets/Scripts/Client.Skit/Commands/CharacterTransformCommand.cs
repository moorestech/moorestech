using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public partial class CharacterTransformCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var character = storyContext.GetCharacter(Character);
            character.SetTransform(Position, Rotation);
            return null;
        }
    }
}

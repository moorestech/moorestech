using Client.Skit.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public partial class CharacterTransformCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var character = storyContext.GetCharacter(Character);
            // JSONの位置はスポーン基準の相対値なので原点を足してワールド座標へ
            // JSON positions are spawn-relative, so add the origin to reach world space
            var origin = storyContext.GetSkitOrigin();
            character.SetTransform(origin.ToWorld(Position), Rotation);
            return null;
        }
    }
}

using Client.Skit.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public partial class CameraWarpCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            storyContext.SkitCamera.SetTransform(Position, Rotation);
            return null;
        }
    }
}

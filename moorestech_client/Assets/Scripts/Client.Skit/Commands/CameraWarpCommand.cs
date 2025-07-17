using Client.Skit.Context;
using CommandForgeGenerator.Command.Util;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public partial class CameraWarpCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var (pos, rot) = CameraUtil.GetCameraTransform(storyContext, CameraOrigin, Position, Rotation, CameraOriginCharacter, CameraOriginBone);
            storyContext.GetSkitCamera().SetTransform(pos, rot);
            return null;
        }
    }
}

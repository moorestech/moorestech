using Client.Skit.Context;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class CameraWarpCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var skitCamera = storyContext.GetSkitCamera();
            // JSONの位置はスポーン基準の相対値なので原点を足してワールド座標へ
            // JSON positions are spawn-relative, so add the origin to reach world space
            var origin = storyContext.GetSkitOrigin();
            skitCamera.SetTransform(origin.ToWorld(Position), Rotation);
            skitCamera.SetFov(FieldOfView);
            
            return null;
        }
    }
}

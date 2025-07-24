using Client.Skit.Context;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class CameraWarpCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var skitCamera = storyContext.GetSkitCamera();
            skitCamera.SetTransform(Position, Rotation);
            skitCamera.SetFov(FieldOfView);
            
            return null;
        }
    }
}
